using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EntityClasses;
using Dapper;
using GeneralUtilsNs;
using Microsoft.Data.SqlClient;
using Serilog;
using ConfigurationNs;
using EiopaConstants;
using Z.Expressions;

namespace Validations
{


    public class RuleStructure
    {
        //***************************************************
        //A Rule Structure contains all the info required to evaluate a validation Rule
        //It has the tablebase formula and the symbol formula
        //  it also contains a LIST of Rule Terms {}
        // the rule has an attribute rowCol which may be the row or the col of the rule. 
        //resmm.SymbolFormula.Should().Be("X0=max(0,min(0.5*X1-3*X2))");
        //resmm.SymbolFinalFormula.Should().Be("X0=Z0");
        //******************************************************

        public ConfigObject ConfigObject { get; set; }
        public List<RuleTerm> RuleTerms = new();
        public List<RuleTerm> FilterTerms = new();


        public int ValidationRuleId { get; private set; } = 0;
        public string TableBaseFormula { get; set; } = "";
        public string FilterFormula { get; set; } = "";

        internal C_ValidationRuleExpression ValidationRuleDb { get; }
        public string SymbolFormula { get; private set; } = "";
        public string SymbolFinalFormula { get; set; } = "";

        public string SymbolFilterFormula { get; set; } = "";
        public string SymbolFilterFinalFormula { get; set; } = "";

        public string ScopeRowCol { get; set; } = "";
        public string ScopeTableCode { get; set; } = "";
        public bool IsValidRule { get; private set; } = false;

        public int DocumentId { get; set; }
        public int SheetId { get; set; }
        public ScopeRangeAxis ApplicableAxis { get; private set; } = ScopeRangeAxis.Error;
        public string ScopeString { get; private set; } = "";


        public RuleStructure(ConfigObject configObject, string tableBaseForumla, string filterFormula = "") : this(tableBaseForumla, filterFormula)
        {
            ConfigObject = configObject;
        }

        public RuleStructure(string tableBaseForumla, string filterFormula = "")
        {
            TableBaseFormula = tableBaseForumla?.Trim();
            FilterFormula = filterFormula?.Trim();

            (SymbolFormula, SymbolFinalFormula, RuleTerms) = CreateSymbolFormulaAndFunctionTerms(TableBaseFormula);
            (SymbolFilterFormula, SymbolFilterFinalFormula, FilterTerms) = CreateSymbolFormulaAndFunctionTerms(FilterFormula);
        }

        private static (string symbolFormula, string finalSymbolFormula, List<RuleTerm> theTerms) CreateSymbolFormulaAndFunctionTerms(string theFormulaExpression)
        {
            //Create symbol formula and finalSymbol formula. Same for filter
            //Two kind of terms: normal and function terms. 
            //fhe SymbolFormula represents the original formula where the normal terms are replaced by X0,X1
            //The SymbolFinalFormula is the symbolFormula but the function terms are now replaced with Z0,Z1,
            //--if there are nested functions  we have nested function terms T1,T2, 
            //formula = @"{S.23.01.01.01,r0540,c0050}=max(0,min(0.5*{S.23.01.01.01,r0580,c0010}-3*{S.23.01.01.01,r0540,c0040})) ";
            //-- NotmalTerms:{S.23.01.01.01,r0540,c0050}
            //-- FunctionTerms min(0.5*{S.23.01.01.01,r0580,c0010}-3*{S.23.01.01.01,r0540,c0040})
            //var resmm = new RuleStructure(formula);
            //resmm.SymbolFormula.Should().Be("X0=max(0,min(0.5*X1-3*X2))");
            //resmm.SymbolFinalFormula.Should().Be("X0=Z0");
            //resmm.RuleTerms.Count.Should().Be(5);


            //Create the plain Terms "X".
            //The formula will change from  min({S.23.01.01.01,r0540,c0040}) to min(X0) . X0 term will be created
            (var symbolFormula, var ruleTerms) = CreateRuleTermsNew(theFormulaExpression);
            var theFormula = symbolFormula;
            var theTerms = ruleTerms;

            //Create the Function Terms "Z".
            //Define FinalSymbolFormula.  "max(0,min(0.5*X0-3*X1))" => with Z0. Z0 term will be created (Z0 may have a nested term)
            (var finalSymbolFormula, var newFunctionTerms) = PrepareFunctionTermsNew(theFormula, "Z");
            var theSymbolFinalFormula = finalSymbolFormula;
            foreach (var newTerm in newFunctionTerms)
            {
                theTerms.Add(newTerm);
            }

            //"Z" function terms may have  nested function terms            
            //if a function Term "Z" has a nested term, the FinalFormula will remain the same but its termText will change from 
            //"X0=max(0,min(0.5*X1-3*X2))")
            var count = 0;
            foreach (var newFunctionTerm in newFunctionTerms)
            {
                //if any of the terms created have a nested term, we need to add the nested and simplify the finalSymbolFormula                
                var internalMatch = RegexValidationFunctions.FunctionTypesRegex.Match(newFunctionTerm.TermText);
                var internalText = internalMatch.Groups[2].Value.Trim();


                //*********************************************************************************
                // a function term may have nested function terms
                // change the function text and create new function term ("T"
                //Term Z0= min(max(X1,3*X1)) => min(T0) and nested term is T0= max(x1,3*x1) is added
                // recursion could have been used , but then I would have to change the evaluate term also            

                (var nestedFormula, var nestedTerms) = PrepareFunctionTermsNew(internalText, $"T{count}");
                newFunctionTerm.TermText = newFunctionTerm.TermText.Replace(internalText, nestedFormula);
                foreach (var nestedTerm in nestedTerms)
                {
                    theTerms.Add(nestedTerm);
                }
                count++;
            }
            return (theFormula, theSymbolFinalFormula, theTerms);
        }

        public static (string symbolExpression, List<RuleTerm>) PrepareFunctionTermsNew(string expression, string termLetter)
        {
            //1.Return a new SymbolExpression with term symbols for each FUNCTION (not term)
            //2 Create one new term  for each function     
            //X0=sum(X1) + sum(X2) => X0=Z0 + Z1 and create two new terms 
            //*** Distinct letters for duplicate terms ***

            if (string.IsNullOrWhiteSpace(expression))
                return ("", new List<RuleTerm>());


            var distinctMatches = RegexValidationFunctions.FunctionTypesRegex.Matches(expression)
                .Select(item => item.Captures[0].Value.Trim()).ToList()
                .Distinct()
                .ToList();


            var ruleTerms = distinctMatches
                .Select((item, Idx) => new RuleTerm($"{termLetter}{Idx:D2}", item, true)).ToList();

            if (ruleTerms.Count == 0)
                return (expression, new List<RuleTerm>());

            var symbolExpression = ruleTerms
                .Aggregate(expression, (currValue, item) => currValue.Replace(item.TermText, item.Letter));
            return (symbolExpression, ruleTerms);
        }

        public static (string symbolExpression, List<RuleTerm>) CreateRuleTermsNew(string expression)
        {

            //an expression is like below. It has terms and functions
            //-- expression:{ S.02.01.30.01,r0030,c0040} = sum({ S.06.02.30.01,c0100,snnn})
            //-- a term :   { S.02.01.30.01,r0030,c0040} Or  { S.06.02.30.01,c0100,snnn} 
            //create a symbol expression like Y1 = Sum(Y2) + Y3 
            //and a list of RuleTerms Y1={S.02.01.30.01,r0030,c0040}, Y2={{ S.06.02.30.01,c0100,snnn} }
            //*** Distinct letters for duplicate terms
            if (string.IsNullOrWhiteSpace(expression))
                return ("", new List<RuleTerm>());

            var distinctMatches = RegexConstants.PlainTermRegEx.Matches(expression)
            .Select(item => item.Captures[0].Value.Trim())
            .Distinct();

            var ruleTerms = distinctMatches
                .Select((item, Idx) => new RuleTerm($"X{Idx}", item, false)).ToList();

            if (ruleTerms.Count == 0)
                return ("", new List<RuleTerm>());

            var symbolExpression = ruleTerms
                .Aggregate(expression, (currValue, item) => currValue.Replace(item.TermText, item.Letter));
            return (symbolExpression, ruleTerms);
        }

        public void SetApplicableAxis(ScopeRangeAxis applicableAxis)
        {
            ApplicableAxis = applicableAxis;
        }

        private RuleStructure()
        {
            //to prevent user from creating default constructor;
        }

        public RuleStructure(C_ValidationRuleExpression validationRuleDb) : this(validationRuleDb.TableBasedFormula, validationRuleDb.Filter)
        {

            if (validationRuleDb is null)
            {
                return;
            }
            ValidationRuleId = validationRuleDb.ValidationRuleID;
            TableBaseFormula = validationRuleDb.TableBasedFormula ?? "";
            FilterFormula = validationRuleDb.Filter ?? "";
            ValidationRuleDb = validationRuleDb;
            ScopeString = ValidationRuleDb.Scope ?? "";
            ScopeTableCode = GetTableCode();

        }

        private string GetTableCode()
        {
            var rxSheetCode = @"(^[A-Z]{1,3}(?:\.\d\d){4})"; //use cnt.TableCode
            var sheetCode = GeneralUtils.GetRegexSingleMatch(rxSheetCode, ScopeString).ToUpper().Trim();
            return sheetCode;

        }

        public RuleStructure Clone()
        {
            var newRule = (RuleStructure)this.MemberwiseClone();

            newRule.RuleTerms = this.RuleTerms
                .Select(term => new RuleTerm(term.Letter, term.TermText, term.IsFunctionTerm)).ToList();

            newRule.FilterTerms = this.FilterTerms
                .Select(term => new RuleTerm(term.Letter, term.TermText, term.IsFunctionTerm)).ToList();

            return newRule;
        }

        public void UpdateTermsWithScopeRowCol(string rowCol)
        {
            //PF.04.03.24.01 (r0040;0050;0060;0070;0080) 
            foreach (var term in RuleTerms)
            {

                if (ApplicableAxis == ScopeRangeAxis.Cols)
                {
                    //if it is an open table 
                    // 1. find the key of the row
                    // 2. find the row of based on the key value
                    // same for filter 
                    term.Col = rowCol;
                }
                else if (ApplicableAxis == ScopeRangeAxis.Rows)
                {
                    term.Row = rowCol;
                }
                else if (ApplicableAxis == ScopeRangeAxis.None)
                {
                    //do nothing
                }

            }
            foreach (var term in FilterTerms)
            {

                if (ApplicableAxis == ScopeRangeAxis.Cols)
                {
                    term.Col = rowCol;
                }
                else if (ApplicableAxis == ScopeRangeAxis.Rows)
                {
                    term.Row = rowCol;
                }
                else if (ApplicableAxis == ScopeRangeAxis.None)
                {

                }

            }
        }

        public static List<RuleTerm> CreateRuleTerms(string expression)
        {
            //a term is something like sum({ PFE.06.02.30.01,c0100,snnn}) or just { PFE.02.01.30.01,r0030,c0040}. it has a function and a value. 
            // value terms get the value from the db
            //{ PFE.02.01.30.01,r0030,c0040} = sum({ PFE.06.02.30.01,c0100,snnn})=> {{ PFE.02.01.30.01,r0030,c0040},sum({ PFE.06.02.30.01,c0100,snnn})}            
            //we will also build the corresponding symbol list (X1, X2) using the index of each term
            //the function sum, isFallback, etc will be converted to proper symbols in AssertExpression

            if (string.IsNullOrWhiteSpace(expression))
            {
                return new List<RuleTerm>();
            }
            //it will capture the terms as listed in the regex, the last one is a plain term without function
            //?: means non-capturing 
            //(?<!) means negative lookbehind
            //usemore capture groups. we do not have or need groups here
            //we get the value of the whole match for every term. We will build the cellCordinate for each term  when new CellCoordinates(term)


            //(?<!{) is a lookbehind of "{" to prevent greedy of previous term
            //?: non-capture to be able to use Matches instead of groups
            //var sumReg = @"(?:sum\(\{.*?(?<!{)\}\))";
            var sumReg = @"((sum)\(\{.*?(?<!{)\}\))";
            var minReg = @"(?:min\(.*\))";  //non greedy to catch min(max
            var maxReg = @"(?:max\(.*\))"; //non greedy to catch max(min
            var countReg = @"(?:count\(\{.*?(?<!{)\}\))";
            var emptyReg = @"(?:empty\(\{.*?(?<!{)\}\))";
            var fdtvReg = @"(?:(matches)\((ftdv\(\{.*?(?<!{)\},.*?\),"".*?""\)))";
            //var matchesReg = @"(?:(matches)\(({.*?(?<!{)},"".*?)""\))";
            var matchesReg = @"((matches)\(({.*?(?<!{)},"".*?)""\))";
            var isFallbackReg = @"(?:isfallback\(\{.*?(?<!{)\}\))";
            var compareEnum = @"({\w{1,3}(?:\.\d\d){4}.*?(?<!{)}\s{0,1}!?=\s{0,1}\[.*?\])";
            var exDimVal = @"(?:ExDimVal\(({.*?(?<!{)}\s*?,\s*?\w\w\)))";
            var xTerm = @"(?:\=\s*?(x\d))";
            var plainReg = @"(?:\{.*?(?<!{)\})";

            var rgxTerms = $@"{countReg}|{sumReg}|{minReg}|{maxReg}|{emptyReg}|{fdtvReg}|{matchesReg}|{isFallbackReg}|{compareEnum}|{exDimVal}|{xTerm}|{plainReg}";
            var rg = new Regex(rgxTerms, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var matches = rg.Matches(expression);
            var termlist = matches.Select(item => item.Value).ToList(); //{PFE.02.01.30.01,r0030,c0040} = sum({ PFE.06.02.30.01,c0100,snnn})=> {{ PFE.02.01.30.01,r0030,c0040},sum({ PFE.06.02.30.01,c0100,snnn})}
            var symbolList = termlist.Select((item, index) => $"X{index }").ToList(); //{ PFE.02.01.30.01,r0030,c0040} = sum({ PFE.06.02.30.01,c0100,snnn})=>{x1,x2}

            var list = termlist.Select((term, index) => new RuleTerm(symbolList[index], termlist[index], false)).ToList();



            return list;

        }

        public bool ValidateTheRule()
        {
            var rule = this;
            //** The rule will be VALID if filter is false   
            //** if the filter is empty Or Valid then check the rule
            //** Rules with a sum function use the filter differently. The filter Takes out rows for the sum function

            if (string.IsNullOrWhiteSpace(rule.SymbolFinalFormula))
            {
                Log.Error($"EMPTY Rule expression {rule.ValidationRuleId}");

                IsValidRule = false;
                return IsValidRule;
            }

            //Check the filter first
            //if the filter is invalid, the rule is valid
            //However, do NOT check the filter if the rule has a sum(SNN) since the filter is used to filter out the rows
            if (!string.IsNullOrWhiteSpace(rule.SymbolFilterFormula) && !rule.TableBaseFormula.ToUpper().Contains("SNN"))
            {
                if (rule.FilterTerms.Any(term => term.IsMissing))
                {
                    IsValidRule = true;
                    return IsValidRule;
                }
                var isFilterValid = AssertIfThenElseExpression(rule.ValidationRuleId, rule.SymbolFilterFinalFormula, rule.FilterTerms);


                if (isFilterValid is null || !(bool)isFilterValid)
                {
                    //filter is invalid, return VALID RULE
                    IsValidRule = true;
                    return IsValidRule;
                }
            }

            var isValidRuleUntyped = AssertIfThenElseExpression(rule.ValidationRuleId, rule.SymbolFinalFormula, rule.RuleTerms);
            var isValidRule = isValidRuleUntyped is not null && (bool)isValidRuleUntyped;
            return isValidRule;

            //IsValidRulex = AssertExpressionNew(rule.ValidationRuleId, rule.SymbolFinalFormula, rule.RuleTerms);
            //return IsValidRule;
        }


        static public object AssertIfThenElseExpression(int ruleId, string symbolExpression, List<RuleTerm> ruleTerms)
        {
            //1. fix  the expression to make it ready for Eval 
            //2. If the expression is if() then(), evaluate the "if" and the "then" separately to allow for decimals

            var fixedSymbolExpression = FixExpression(symbolExpression);

            if (string.IsNullOrWhiteSpace(fixedSymbolExpression))
            {
                return null;
            }
            if (ruleTerms.Count == 0)
            {
                return null;
            }


            var (isIfExpressionType, ifExpression, thenExpression) = SplitIfThenElse(fixedSymbolExpression);
            if (isIfExpressionType)
            {
                var validSimplifiedIf = SimplifiedExpression.Process(ruleId, ruleTerms, ifExpression,true).IsValid;
                //var isIfPartTrue = (bool) AssertSingleExpression(ruleId, ifExpression, ruleTerms);

                //if (!(bool)isIfPartTrue)
                //{
                //    return true; 
                //}
                if (!validSimplifiedIf)
                {
                    return true;
                }

                var validSimplifiedThen = SimplifiedExpression.Process(ruleId, ruleTerms, thenExpression,true).IsValid;
                //var isThenPartValid = (bool)AssertSingleExpression(ruleId, thenExpression, ruleTerms);
                
                return validSimplifiedThen;
            }

            var validSimplifiedWhole = SimplifiedExpression.Process(ruleId, ruleTerms, fixedSymbolExpression,true).IsValid;
            //var isWholeValid = (bool)AssertSingleExpression(ruleId, fixedSymbolExpression, ruleTerms);
            
            return validSimplifiedWhole;
        }


        static (bool isIfExpression, string ifExpression, string thenExpression) SplitIfThenElse(string stringExpression)
        {
            //split if then expression            
            //if(A) then B=> A, B            

            var rgxIfThen = @"if\s*(.*)\s*then(.*)";
            //var rgxIfThen = @"if\s*\((.*)\)\s*then(.*)";

            var terms = GeneralUtils.GetRegexSingleMatchManyGroups(rgxIfThen, stringExpression);
            if (terms.Count != 3)
            {
                return (false, "", "");
            }


            return (true, terms[1], terms[2]);
        }

        public static string FixExpression(string symbolExpression)
        {
            var qt = @"""";
            var fixedExpression = symbolExpression;
            //convert if then statements to an expression
            //and change the symbles not=> !, and =>&& , etc                                    

            fixedExpression = fixedExpression.Replace("=", "==");
            fixedExpression = fixedExpression.Replace("!==", "!=");
            fixedExpression = fixedExpression.Replace("<==", "<=");
            fixedExpression = fixedExpression.Replace(">==", ">=");

            fixedExpression = $"{fixedExpression} ";

            var myEvaluator = new MatchEvaluator(PutQuotesAroundTerm);

            fixedExpression = Regex.Replace(fixedExpression, @"=\s?(x\d{1,3})[\s$""]", myEvaluator);// x0=>"x0"

            fixedExpression = fixedExpression.Replace("[", qt);//used for domain members which are strings
            fixedExpression = fixedExpression.Replace("]", qt);

            fixedExpression = fixedExpression.Replace("not", "!");
            fixedExpression = fixedExpression.Replace("and", "&&");
            fixedExpression = fixedExpression.Replace("or", "||");

            return fixedExpression;

            static string PutQuotesAroundTerm(Match m)
            {//two groups: 1 for the whole and one group for the parenthesis

                if (m.Groups.Count != 2)
                {
                    return m.Value;
                }

                //replaces the match in entire match group =x0 => ="x0"
                var newVal = m.Value.Replace(m.Groups[1].Value, $"\"{m.Groups[1]}\"");
                return newVal;
            }
        }

    }
}
