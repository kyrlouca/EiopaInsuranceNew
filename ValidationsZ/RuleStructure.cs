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
        public bool IsTechnical { get; set; } = false;
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


        private RuleStructure()
        {
            //to prevent user from creating default constructor;
        }


        public RuleStructure(string tableBaseFormula, string filterFormula, bool isTechnical, C_ValidationRuleExpression validationRuleDb)
        {
            if (validationRuleDb is not null)
            {

                ValidationRuleId = validationRuleDb.ValidationRuleID;
                TableBaseFormula = validationRuleDb.TableBasedFormula?.Trim() ?? "";
                FilterFormula = validationRuleDb.Filter?.Trim() ?? "";
                ValidationRuleDb = validationRuleDb;
                ScopeString = ValidationRuleDb.Scope?.Trim() ?? ""; //scopestring not needed
                ScopeTableCode = GetTableCode();
                IsTechnical = validationRuleDb.ValidationCode.StartsWith("TV");
            }
            else
            {
                //this is used for testing where only the formula is required and NOT the rule record from the Db
                TableBaseFormula = tableBaseFormula?.Trim() ?? "";
                FilterFormula = filterFormula.Trim() ?? "";
                IsTechnical = isTechnical;
            }


            if (IsTechnical)
            {
                (SymbolFormula, SymbolFinalFormula, RuleTerms) = CreateSymbolFormulaAndFunctionTermsTechincalRule(TableBaseFormula);
            }
            else
            {
                (SymbolFormula, SymbolFinalFormula, RuleTerms) = CreateSymbolFormulaAndFunctionTerms(TableBaseFormula);
                (SymbolFilterFormula, SymbolFilterFinalFormula, FilterTerms) = CreateSymbolFormulaAndFunctionTerms(FilterFormula);
            }

        }

        public RuleStructure(string tableBaseFormula, string filterFormula)
        {

            //This constructor can be used for testing because it only requires the TableBaseFormula string
            //It is used as the first Parent in chained constructors
            TableBaseFormula = tableBaseFormula?.Trim();
            FilterFormula = filterFormula?.Trim();

            if (IsTechnical)
            {
                (SymbolFormula, SymbolFinalFormula, RuleTerms) = CreateSymbolFormulaAndFunctionTermsTechincalRule(TableBaseFormula);
            }
            else
            {
                (SymbolFormula, SymbolFinalFormula, RuleTerms) = CreateSymbolFormulaAndFunctionTerms(TableBaseFormula);
                (SymbolFilterFormula, SymbolFilterFinalFormula, FilterTerms) = CreateSymbolFormulaAndFunctionTerms(FilterFormula);
            }

        }



        public RuleStructure(C_ValidationRuleExpression validationRuleDb) : this(validationRuleDb.TableBasedFormula, validationRuleDb.Filter)
        {
            //this is the constructions which is actually used for assertions becaused it has the rule record as read from the db
            //it will execure the chained this first.
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


        private static (string symbolFormula, string finalSymbolFormula, List<RuleTerm> theTerms) CreateSymbolFormulaAndFunctionTerms(string theFormulaExpression)
        {
            //Create symbol formula and finalSymbol formula. Same for filter
            //Two kind of terms: normal and function terms. 
            //fhe SymbolFormula represents the original formula where the normal terms are replaced by X00,X01
            //The SymbolFinalFormula is the symbolFormula but the function terms are now replaced with Z0,Z1,
            //--if there are nested functions  we have nested function terms T01,T02, ,the first digit is from its father Z letter
            //formula = @"{S.23.01.01.01,r0540,c0050}=max(0,min(0.5*{S.23.01.01.01,r0580,c0010}-3*{S.23.01.01.01,r0540,c0040})) ";
            //-- NotmalTerms:{S.23.01.01.01,r0540,c0050}
            //-- FunctionTerms min(0.5*{S.23.01.01.01,r0580,c0010}-3*{S.23.01.01.01,r0540,c0040})
            //var resmm = new RuleStructure(formula);
            //resmm.SymbolFormula.Should().Be("X00=max(0,min(0.5*X01-3*X02))");
            //resmm.SymbolFinalFormula.Should().Be("X00=Z00");  Z00 = max(0,T01) and T01=min(0.5*X01-3*X02)
            //resmm.RuleTerms.Count.Should().Be(5);




            //*********************************************************************************
            //Create the plain Terms "X".
            //The formula will change from  min({S.23.01.01.01,r0540,c0040}) to min(X00) . X00 term will be created
            (var symbolFormula, var ruleTerms) = CreateRuleTermsNew(theFormulaExpression);
            var theFormula = symbolFormula;
            var theTerms = ruleTerms;

            //*********************************************************************************
            //Create the Function Terms "Z".
            //Define FinalSymbolFormula.  "max(0,min(0.5*X0-3*X1))" => with Z00. Z00 term will be created (Z00 may have a nested term) Z00 = max(0,T01) and T01=min(0.5*X01-3*X02)
            (var finalSymbolFormula, var newFunctionTerms) = PrepareFunctionTermsNew(theFormula, "Z");
            var theSymbolFinalFormula = finalSymbolFormula;
            foreach (var newTerm in newFunctionTerms)
            {
                theTerms.Add(newTerm);
            }


            //*********************************************************************************
            //"Z" function terms may have  nested function terms            
            //if a function Term "Z" has a nested term, the FinalFormula will remain the same but its termText will have a T00 term
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


        private static (string symbolFormula, string finalSymbolFormula, List<RuleTerm> theTerms) CreateSymbolFormulaAndFunctionTermsTechincalRule(string theFormulaExpression)
        {
            //Create symbol formula and finalSymbol formula. Same for filter
            //Two kind of terms: normal and function terms. 

            //*********************************************************************************
            //Create the plain Terms "X".
            //The formula will change from  min({S.23.01.01.01,r0540,c0040}) to min(X00) . X00 term will be created
            (var symbolFormula, var ruleTerms) = CreateRuleTermsNew(theFormulaExpression);
            var theFormula = symbolFormula;
            var theTerms = ruleTerms;

            //*********************************************************************************
            //Create the Function Terms "Z".
            //Define FinalSymbolFormula.  "max(0,min(0.5*X0-3*X1))" => with Z00. Z00 term will be created (Z00 may have a nested term) Z00 = max(0,T01) and T01=min(0.5*X01-3*X02)
            (var finalSymbolFormula, var newFunctionTerms) = PrepareFunctionTermsTechnical(theFormula, "Z");
            var theSymbolFinalFormula = finalSymbolFormula;
            foreach (var newTerm in newFunctionTerms)
            {
                theTerms.Add(newTerm);
            }
            return (theFormula, theSymbolFinalFormula, theTerms);
        }



        public static (string symbolExpression, List<RuleTerm>) PrepareFunctionTermsNew(string expression, string termLetter)
        {
            //1.Return a new SymbolExpression with term symbols for each FUNCTION (not term)
            //2 Create one new term  for each function     
            //X0=sum(X1) + sum(X2) => X0=Z0 + Z1 and create two new terms 
            //*** Same distinct letter for exactly the same terms ***

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
                .Select((item, Idx) => new RuleTerm($"X{Idx:D2}", item, false)).ToList();

            if (ruleTerms.Count == 0)
                return ("", new List<RuleTerm>());

            var symbolExpression = ruleTerms
                .Aggregate(expression, (currValue, item) => currValue.Replace(item.TermText, item.Letter));
            return (symbolExpression, ruleTerms);
        }


        public static (string symbolExpression, List<RuleTerm>) PrepareFunctionTermsTechnical(string expression, string termLetter)
        {
            //1.Return a new SymbolExpression with term symbols for each FUNCTION (not term)
            //2 Create one new term  for each function     
            //X0=sum(X1) + sum(X2) => X0=Z0 + Z1 and create two new terms 
            //*** Same distinct letter for exactly the same terms ***

            if (string.IsNullOrWhiteSpace(expression))
                return ("", new List<RuleTerm>());

            var technicalRegex = new Regex(@"(.*?).\s*like\s*('.*')", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var distinctMatches = technicalRegex.Matches(expression)
                .Select(item => item.Captures[0].Value.Trim()).ToList()
                .Distinct()
                .ToList();

            var ruleTerms = distinctMatches
                .Select((item, Idx) => new RuleTerm($"{termLetter}{Idx:D2}", item, true))
                .ToList();                                
           
            var symbolExpression = ruleTerms
                .Aggregate(expression, (currValue, item) => currValue.Replace(item.TermText, item.Letter));

            ruleTerms.ForEach(term => TransformTechnicalTerm(term));
            return (symbolExpression, ruleTerms);
            static RuleTerm TransformTechnicalTerm(RuleTerm term)
            {
                //

                term.DataTypeOfTerm = DataTypeMajorUU.BooleanDtm;
                term.FunctionType = FunctionTypes.LIKE;

                var expression = "";
                var regEx = @"(.*).\s*like\s*('.*')";
                var parts = GeneralUtils.GetRegexSingleMatchManyGroups(regEx, term.TermText);

                expression = (parts.Count != 3)
                    ? ""
                    : $"LIKE({parts[1]},{parts[2]})";

                term.TermText = expression;
                return term;

            }
        }




        public void SetApplicableAxis(ScopeRangeAxis applicableAxis)
        {
            ApplicableAxis = applicableAxis;
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
                var validSimplifiedIf = SimplifiedExpression.Process(ruleId, ruleTerms, ifExpression, true).IsValid;
                //var isIfPartTrue = (bool) AssertSingleExpression(ruleId, ifExpression, ruleTerms);

                //if (!(bool)isIfPartTrue)
                //{
                //    return true; 
                //}
                if (!validSimplifiedIf)
                {
                    return true;
                }

                var validSimplifiedThen = SimplifiedExpression.Process(ruleId, ruleTerms, thenExpression, true).IsValid;
                //var isThenPartValid = (bool)AssertSingleExpression(ruleId, thenExpression, ruleTerms);

                return validSimplifiedThen;
            }

            var validSimplifiedWhole = SimplifiedExpression.Process(ruleId, ruleTerms, fixedSymbolExpression, true).IsValid;
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

        private static string FixExpression(string symbolExpression)
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
