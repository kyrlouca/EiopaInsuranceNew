﻿using EiopaConstants;
using GeneralUtilsNs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using System.Threading.Tasks;
using Z.Expressions;
using Serilog;

namespace Validations
{
    //to reduce the complexity of the RuleStructure we create this class for the evaluation
    // convert the ruleterms into objects (which also have decimals for tolerance)
    // Unfortunately, we still need the terms
    public class TermExpression
    {
        public string LetterId { get; set; }
        public string TermExpressionStr { get; set; }
        public bool IsValid { get; set; }
    }

    public class SimplifiedExpression
    {
        public bool IsTesting { get; set; }
        public string LetterId { get; set; }
        public int RuleId { get; set; }
        public string Expression { get; set; }
        public static List<RuleTerm> RuleTerms { get; set; }
        public string SymbolExpressionFinal { get; set; } = "";

        public bool IsValid { get; set; }
        public List<TermExpression> TermExpressions { get; set; } = new(); //x2>=X1+X2
        public List<SimplifiedExpression> PartialSimplifiedExpressions { get; set; } = new(); //make it a list  (x2>=X1+X2 && X3>3)         
        private SimplifiedExpression() { }
        private static int SECounter { get; set; } = 0;
        private static int TECounter { get; set; } = 0;
        public static Dictionary<string, ObjTerm> TolerantObjValues { get; set; }
        public static Dictionary<string, object> PlainObjValues { get; set; }


        public static SimplifiedExpression Process(int ruleId, List<RuleTerm> ruleTerms, string expression, bool comesFromUser, bool isTesting = false)
        {
            //********************* This is a recursive Procedure*********************************            
            // Take an expression and evaluate to true or false
            // However, an expression may consists of other expressions and terms
            // So, this static function will find other simplified expressions within the simplified expression and also the terms of the expression
            // the simplified expressinons are  replaced with letter
            // the results of both simplified expressions and terms are stored as plain objects (they have just a value and the decimals for tolerance)
            // ** recursion is used.
            // ** A term is just X2>=X1+X2 and it does NOT have any && or || . It can be asserted using tolerances
            //  initial simplified : (x2>=X1+X2 && X3>3) || X1>4
            //  => SE01 || X1>4  where SE01 is a recursed simplified
            //        SE01= (x2>=X1+X2 && X3>3) and has two terms 

            //**********************************************************************************

            if (comesFromUser)
            {
                //initilize the counters the first time before recursion
                SECounter = 0;
                TECounter = 0;
                PlainObjValues = new();
                TolerantObjValues = new();
                RuleTerms = new();
            }
            //a simplified expression will have its letterId like SE01
            var se = new SimplifiedExpression(ruleId, ruleTerms, expression, comesFromUser, isTesting);

            //create *recursively* the simplifiedExpressions within the simplifed expression (they are in parenthesis)
            var newFormula = se.Expression;
            se.PartialSimplifiedExpressions = se.CreatePartialSimplifiedExpressions();
            se.SymbolExpressionFinal = se.PartialSimplifiedExpressions
               .Aggregate(newFormula, (currValue, partialSimplified) => currValue.Replace(partialSimplified.Expression, $" {partialSimplified.LetterId} "))
               .Trim();

            //now create the terms
            se.TermExpressions = se.CreateTermExpressions();
            se.SymbolExpressionFinal = se.TermExpressions
                .Aggregate(se.SymbolExpressionFinal, (currValue, termExpression) => currValue.Replace(termExpression.TermExpressionStr, $" {termExpression.LetterId} "))
                .Trim();

            if (!isTesting)// testing is used to unit test how expressions are recursively built without the need of rule terms
                se.AssertSimplified();//it will arrive here after all recursed simplified where asserted!
            return se;
        }


        private SimplifiedExpression(int ruleId, List<RuleTerm> ruleTerms, string expression, bool comesFromUser, bool isTesting = false)
        {
            RuleId = ruleId;
            RuleTerms = ruleTerms;
            Expression = expression ?? "";            
            LetterId = $"SE{SimplifiedExpression.SECounter++:D2}";
            if (comesFromUser)
            {
                //assertion will use object terms, not rule terms. Therefore, the first time create object terms from rule terms.

                TolerantObjValues = CreateObjectTerms(ruleTerms);
                PlainObjValues = TolerantObjValues.ToDictionary(objt => objt.Key, objt => objt.Value.obj);
            }
            IsTesting = isTesting;
        }


        private void AssertSimplified()
        {
            //it will assert all the terms and all the recursed  simplifed 
            //to debug check the AssertSingleTemrExpression

            //Assert the terms first (X1>X2). Cannot assert first recursed expressions which contain terms
            foreach (var termExpression in TermExpressions)
            {
                //DEBUG here
                var isValidTerm = AssertTerm(termExpression.TermExpressionStr);

                var isBooleanType = Regex.Match(termExpression.TermExpressionStr, @"(>|<|==)").Success;
                termExpression.IsValid = isBooleanType ? (bool)isValidTerm : false;

                PlainObjValues.Add(termExpression.LetterId, isValidTerm);
            }

            //Assert the partial expressions (which may contain other partial expressions  as this is recursive)
            foreach (var partialSimplifiedExpression in PartialSimplifiedExpressions)
            {
                var isValidPartialSimplified = AssertTerm(SymbolExpressionFinal);
                try
                {
                    //if the result of the term expression is a number isnstead of a bool, it means that the whole rule was wrong and had a mix of bools and numbers
                    partialSimplifiedExpression.IsValid = (bool)isValidPartialSimplified;
                }
                catch (Exception ex)
                {
                    Log.Error($"{RuleId}--{ex.Message}");
                    Console.WriteLine(ex.Message);
                    partialSimplifiedExpression.IsValid = false;
                }

            }
            var result = Eval.Execute(SymbolExpressionFinal, PlainObjValues);
            IsValid = result.GetType() == typeof(bool) ? IsValid = (bool)result : true;
            PlainObjValues.Add(LetterId, result);// the recursed will be asserted first and it by now they will be inserted as plain objects
        }


        public List<SimplifiedExpression> CreatePartialSimplifiedExpressions()//the partial are recursed expressions
        {
            var partialSimplifiedExpressions = new List<SimplifiedExpression>();
            if (string.IsNullOrWhiteSpace(Expression))
                return partialSimplifiedExpressions;

            var ParenthesisPartialRegStr = @$"\((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!))\)";
            Regex ParenthesisPartialReg = new(ParenthesisPartialRegStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var cleanExpression = RemoveOuterParenthesis(Expression);
            var distinctMatches = ParenthesisPartialReg.Matches(cleanExpression)
                .Select(item => item.Captures[0].Value.Trim())
                .Distinct();


            var partialSimplified = distinctMatches
                .Select(expr => SimplifiedExpression.Process(RuleId, RuleTerms, expr, false, IsTesting))
                .ToList();

            return partialSimplified;
        }


        public List<TermExpression> CreateTermExpressions()
        {
            var termExpressions = new List<TermExpression>();
            if (string.IsNullOrWhiteSpace(SymbolExpressionFinal))
                return termExpressions;

            var cleanExpression = RemoveOuterParenthesis(SymbolExpressionFinal);
            var terms = cleanExpression.Split(new string[] { "&&", "||" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (var term in terms)
            {
                termExpressions.Add(new TermExpression() { LetterId = $"VV{TECounter++:D2}", TermExpressionStr = term.Trim() });
            }


            var partial = termExpressions
                .Where(pe => !pe.TermExpressionStr.StartsWith("SE"))
                .ToList();
            return partial;
        }

        public object AssertTerm(string expression)
        {
            
            //it can return a boolean or a value
            object result;
            //*** first assert expression with eval without tolerances
            try
            {               
                result = Eval.Execute(expression, PlainObjValues);
            }
            catch (Exception e)
            {
                var mess = e.Message;
                Console.WriteLine(mess);
                Log.Error($"Rule Id:{RuleId} => INVALID Rule expression {mess}");
                throw;
            }
            if (result.GetType() == typeof(bool))
            {
                if ((bool)result)
                    return result;
            }

            //another go, now check equality with tolerance if appropriate
            var peLetters = GeneralUtils.GetRegexListOfMatchesWithCase(@"([XZT]\d{1,2})", expression).Distinct();// get X0,X1,Z0,... from expression and then get only the terms corresponding to these

            var teObjTerms = TolerantObjValues.Where(obj => peLetters.Contains(obj.Key)).ToDictionary(item => item.Key, item => item.Value);
            var isAllDouble = teObjTerms.All(obj => obj.Value.obj?.GetType() == typeof(double));
            //var teDerivedTerms= 

            var teRuleTerms = RuleTerms.Where(rt => peLetters.Contains(rt.Letter));
            var hasFunctionTerm = teRuleTerms.Any(term => term.IsFunctionTerm);  //sum, max, min
            var hasCalculationTerm = Regex.IsMatch(expression, @"SE|PS|VV");
            var (isAlgebraig, leftOperand, operatorUsed, rightOperand) = SplitAlgebraExpresssionNew(expression);

            //for functions or recursed simplified expressions, allow a tolerance of 2 decimal digits (0.005)
            // get S or V  from expression which are NOT plain terms but functions or simplified expr
            var ddLetters = GeneralUtils.GetRegexListOfMatchesWithCase(@"([SV].\d\d)", expression).Distinct();
            var teObjDerived = PlainObjValues.Where(obj => ddLetters.Contains(obj.Key)).ToDictionary(item => item.Key, item => item.Value);
            foreach (var teObjDer in teObjDerived)
            {
                teObjTerms.Add(teObjDer.Key, new ObjTerm() { obj = teObjDer.Value, decimals = 2 });
            }

            //allow tolerance even if term has only two operands (x1=x2). before if it had only two operands we needed an operator such as * or / by a number such as x1= 0.2 * X2            
            if (isAllDouble && isAlgebraig && operatorUsed.Contains("=") )
            {
                result = (bool)IsNumbersEqualWithTolerances(teObjTerms, leftOperand, rightOperand);
            }
            return result;

        }

        static (bool isValid, string leftOperand, string operatorUsed, string rightOperand) SplitAlgebraExpresssionNew(string expression)
        {
            //var containsLogical = Regex.IsMatch(expression, @"[!|&]");
            if (string.IsNullOrEmpty(expression))
            {
                return (false, "", "", "");
            }

            var partsSplit = expression.Split(new string[] { ">=", "<=", "==", ">", "<" }, StringSplitOptions.RemoveEmptyEntries);
            if (partsSplit.Length == 2)
            {
                var left = partsSplit[0].Trim();
                var right = partsSplit[1].Trim();
                var regOps = @"(<=|>=|==|<|>)";
                var oper = GeneralUtils.GetRegexSingleMatch(regOps, expression);
                return (true, left, oper, right);
            }

            return (false, "", "", "");

        }

        private static Dictionary<string, ObjTerm> CreateObjectTerms(List<RuleTerm> ruleTerms)
        {
            Dictionary<string, ObjTerm> xobjTerms = new();

            //var letters = GeneralUtils.GetRegexListOfMatchesWithCase(@"([XZT]\d{1,2})", formula).Distinct();// get X0,X1,Z0,... to avoid x0 

            //var xxTerms = terms.Where(rt => letters.Contains(rt.Letter)).ToList();
            if (ruleTerms is null)
            {
                return xobjTerms;
            }

            foreach (var term in ruleTerms)
            {
                ObjTerm objTerm;
                if (term.IsMissing)
                {
                    objTerm = new ObjTerm
                    {
                        obj = term.DataTypeOfTerm switch
                        {
                            DataTypeMajorUU.BooleanDtm => false,
                            DataTypeMajorUU.StringDtm => "",
                            DataTypeMajorUU.DateDtm => new DateTime(2000, 1, 1),
                            DataTypeMajorUU.NumericDtm => Convert.ToDouble(0.00),
                            _ => term.TextValue,
                        },
                        decimals = term.NumberOfDecimals,
                    };
                }
                else
                {
                    objTerm = new ObjTerm
                    {
                        obj = term.DataTypeOfTerm switch
                        {
                            DataTypeMajorUU.BooleanDtm => term.BooleanValue,
                            DataTypeMajorUU.StringDtm => term.TextValue,
                            DataTypeMajorUU.DateDtm => term.DateValue,
                            //DataTypeMajorUU.NumericDtm => Math.Round( Convert.ToDouble(term.DecimalValue),5),
                            DataTypeMajorUU.NumericDtm => Convert.ToDouble(Math.Truncate(term.DecimalValue * 100000) / 100000), // truncate to 3 decimals
                            _ => term.TextValue,
                        },
                        decimals = term.NumberOfDecimals,
                    };

                }

                if (!xobjTerms.ContainsKey(term.Letter))
                {
                    xobjTerms.Add(term.Letter, objTerm);
                }

            }
            return xobjTerms;
        }


        private static object IsNumbersEqualWithTolerances(Dictionary<string, ObjTerm> tolerantValues, string leftOperand, string rightOperand)
        {
            //we need to remove parenthesis for tolerances to work because if there is a minus outside a parenthesis the small value is the big number
            //left site
            if (leftOperand.Contains("("))
            {
                leftOperand = FlattenExpressionWithoutParenthesis(leftOperand);
            }
            var leftTerms = GetLetterTerms(leftOperand);
            var dicLeftSmall = ConvertDictionaryUsingInterval(leftTerms, tolerantValues, "S");
            var dicLeftLarge = ConvertDictionaryUsingInterval(leftTerms, tolerantValues, "A");

            var leftNumSmall = Convert.ToDouble(Eval.Execute(leftOperand, dicLeftSmall));
            var leftNumBig = Convert.ToDouble(Eval.Execute(leftOperand, dicLeftLarge));
            (leftNumSmall, leftNumBig) = SwapSmaller(leftNumSmall, leftNumBig);

            //Right site
            if (rightOperand.Contains("("))
            {
                rightOperand = FlattenExpressionWithoutParenthesis(rightOperand);
            }
            var rightTerms = GetLetterTerms(rightOperand);
            var dicRightSmall = ConvertDictionaryUsingInterval(rightTerms, tolerantValues,"S" );
            var dicRightLarge = ConvertDictionaryUsingInterval(rightTerms, tolerantValues,"A");

            var rightNumSmall = Convert.ToDouble(Eval.Execute(rightOperand, dicRightSmall));
            var rightNumBig = Convert.ToDouble(Eval.Execute(rightOperand, dicRightLarge));
            (rightNumSmall, rightNumBig) = SwapSmaller(rightNumSmall, rightNumBig);


            var dicLeftNormal = ConvertDictionaryUsingInterval(leftTerms, tolerantValues, "");
            var dicRightNormal = ConvertDictionaryUsingInterval(rightTerms, tolerantValues, "");

            var leftNumNormal = Convert.ToDouble(Eval.Execute(leftOperand, dicLeftNormal));
            var rightNumNormal = Convert.ToDouble(Eval.Execute(rightOperand, dicRightNormal));
            
            var isSmallDifference = Math.Abs(leftNumNormal - rightNumNormal) < 2.0;
            if (isSmallDifference)
            {
                Console.WriteLine("small");
            }

            var isValid = (leftNumSmall <= rightNumBig && leftNumBig >= rightNumSmall) || isSmallDifference;
            return isValid;
        }


        public static Dictionary<string, double> ConvertDictionaryUsingInterval(List<string> letters, Dictionary<string, ObjTerm> normalDic, string addOrSubtract)
        {
            // tolerance type can result to either Big or Small number            
            // signedNum: if it's a negative number, we need to make the number smaller to get the maximum interval

            var newDictionary = new Dictionary<string, double>();
            foreach (var letter in letters)
            {                
                var signedNum = letter.Contains("-") ? -1.0 : 1.0; //for negative we make substraction
                var newLetter = letter.Replace("-", "").Trim();
                var objItem = normalDic[newLetter];
                var power = objItem.decimals;

                try
                {
                    var num = Convert.ToDouble(objItem.obj);
                    var interval = Math.Pow(10, -power) / 2.0;
                    
                    var newNum = num;
                    if(addOrSubtract == "A")
                    {
                        newNum =  num + interval * signedNum;
                    }
                    else if(addOrSubtract == "S")
                    {
                        newNum =  num - interval * signedNum;
                    }                    

                    //var newNum = isAddInterval ? num + interval * signedNum : num - interval * signedNum;
                    newDictionary.Add(newLetter, newNum);
                }
                catch
                {
                    newDictionary.Add(newLetter, 0);
                    Log.Error($"Conversion Error for Exp;{objItem.obj}");
                    Console.WriteLine($"Conversion Error for Exp;{objItem.obj}");
                }

            }

            return newDictionary;

        }


        public static string FlattenExpressionWithoutParenthesis(string expression)
        {

            //remove parenthesis to use smaller and larger tolerances
            //@"$c = $d - (-$e - $f + x2)";=>@"$c = $d + $e + $f - x2";
            var wholeParen = GeneralUtils.GetRegexSingleMatch(@"(-\s*\(.*?\))", expression);
            if (string.IsNullOrEmpty(wholeParen))
            {
                //to catch (x1*x3) without the minus sign
                return expression;
            }
            var x1 = wholeParen.Replace("+", "?");
            var x2 = x1.Replace("-", "+");
            var x3 = x2.Replace("?", "-");
            var x4 = x3.Replace("(", "");
            var x5 = x4.Replace(")", "");//do not replace if string is empty
            var nn = expression.Replace(wholeParen, x5);
            var n1 = Regex.Replace(nn, @"\-\s*\-", "+");
            var n2 = Regex.Replace(n1, @"\+\s*\+", "+");
            var n3 = Regex.Replace(n2, @"\+\s*?\-", "-");

            return n3;
        }


        public static (double, double) SwapSmaller(double a, double b)
        {
            if (a < b)
            {
                return (a, b);
            }
            else
            {
                return (b, a);
            }
        }


        public static List<string> GetLetterTerms(string expression)
        {
            //it will return the letter terms but with the MINUS sign in front
            var list = GeneralUtils.GetRegexListOfMatchesWithCase(@"((?:[XZ]\d{1,2})|(?:SE\d\d)|(?:PS\d\d)|(?:VV\d{1,2}))", expression);
            return list;
        }

        public static string RemoveOuterParenthesis(string expression)
        {

            expression = expression?.Trim() ?? "";

            var balancedParenRegexStr = @$"\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)";
            Regex balancedParenRegex = new(balancedParenRegexStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var match = balancedParenRegex.Match(expression);
            //to avoid geting only (abc) from  (abc)+ (bc)
            var val = match.Success && match.Captures[0].Value == expression
                ? match.Groups[1].Value
                : expression;

            return val;

        }

    }
}
