using EiopaConstants;
using GeneralUtilsNs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Z.Expressions;

namespace Validations
{

    public class PartialExpression
    {
        public string Letter { get; set; }
        public string Expression { get; set; }
        public bool IsValid { get; set; }
    }

    public class SimplifiedExpression
    {
        public int RuleId { get; set; }
        public string Expression { get; set; }
        public string SymbolExpression { get; set; } = "";
        public Dictionary<string, ObjTerm> ObjTerms { get; set; } = new();
        public bool IsValid { get; set; }
        public List<PartialExpression> PartialExpressions { get; set; } = new();
        private SimplifiedExpression() { }

        public static SimplifiedExpression CreateExpression(string expression)
        {
            var se = new SimplifiedExpression(expression);
            se.CreateTerms();
            return se;
        }
        private SimplifiedExpression(string expression)
        {
            Expression = expression;
        }


        public void CreateTerms()
        {
            if (string.IsNullOrWhiteSpace(Expression))
                return;

            var newFormula = Expression;

            var terms = Expression.Split(new string[] { "&&", "||" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var count = 0;
            foreach (var term in terms)
            {
                PartialExpressions.Add(new PartialExpression() { Letter = $"VV{count}", Expression = term.Trim() });
                count += 1;
            }

            SymbolExpression = PartialExpressions
                .Aggregate(newFormula, (currValue, termExpression) => currValue.Replace(termExpression.Expression, $" {termExpression.Letter} "))
                .Trim();


        }
        public bool AssertExperssion(int ruleId, List<RuleTerm> ruleTerms)
        {
            RuleId = ruleId;
            ObjTerms = CreateObjectTerms(ruleTerms);

            //*** first assert expression using eval.
            try
            {
                IsValid = (bool)Eval.Execute(Expression, ObjTerms);
            }
            catch (Exception e)
            {
                var mess = e.Message;
                Console.WriteLine(mess);
                //Log.Error($"Rule Id:{ruleId} => INVALID Rule expression {symbolExpression}\n{e.Message}");
                throw;
            }

            if (IsValid)
                return IsValid;

            //*** If invalid, give another go. Check every partial expression and check with equality tolerance where appropriate
            foreach (var partialExpression in PartialExpressions)
            {
                var peLetters = GeneralUtils.GetRegexListOfMatchesWithCase(@"([XZT]\d{1,2})", partialExpression.Expression).Distinct();// get X0,X1,Z0,... from expression and then get only the terms corresponding to these
                var teObjTerms = ObjTerms.Where(obj => peLetters.Contains(obj.Key)).ToDictionary(item => item.Key, item => item.Value);
                var isAllDouble = teObjTerms.All(obj => obj.Value.obj?.GetType() == typeof(double));

                var teRuleTerms = ruleTerms.Where(rt => peLetters.Contains(rt.Letter));
                var hasFunctionTerm = teRuleTerms.Any(term => term.IsFunctionTerm);  //sum, max, min

                var (isAlgebraig, leftOperand, operatorUsed, rightOperand) = SplitAlgebraExpresssionNew(partialExpression.Expression);

                //check equality with tolerance
                if (isAlgebraig && operatorUsed.Contains("=") && (teObjTerms.Count() > 2 || hasFunctionTerm || partialExpression.Expression.Contains("*")))//only if more than two terms unless there is another term when formula contains *
                {
                    partialExpression.IsValid = (bool)IsNumbersEqualWithTolerances(teObjTerms, leftOperand, rightOperand);
                }
                else
                {
                    //check partial expression using eval
                    try
                    {
                        IsValid = (bool)Eval.Execute(partialExpression.Expression, ObjTerms);
                    }
                    catch (Exception e)
                    {
                        var messs2 = $"Rule Id:{ruleId} => INVALID Rule expression {partialExpression.Expression}\n{e.Message}";
                        var mess = e.Message;
                        Console.WriteLine(mess);
                        //Log.Error($"Rule Id:{ruleId} => INVALID Rule expression {symbolExpression}\n{e.Message}");
                        throw;
                    }
                }                                
            }


            //*** Now evaluate the outcome of all PartialExpressions
            var peObjTerms = PartialExpressions.ToDictionary(pe => pe.Letter, pe => pe.IsValid);
            try
            {
                IsValid = (bool)Eval.Execute(Expression, peObjTerms);
            }
            catch (Exception e)
            {
                var messs2 = $"Rule Id:{ruleId} => INVALID Rule expression {Expression}\n{e.Message}";
                var mess = e.Message;
                Console.WriteLine(mess);
                //Log.Error($"Rule Id:{ruleId} => INVALID Rule expression {symbolExpression}\n{e.Message}");
                throw;
            }
            return IsValid;

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
                            DataTypeMajorUU.NumericDtm => Convert.ToDouble(Math.Truncate(term.DecimalValue * 1000) / 1000), // truncate to 3 decimals
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


        private static object IsNumbersEqualWithTolerances(Dictionary<string, ObjTerm> dicObj, string leftOperand, string rightOperand)
        {
            //interval comparison if equality operator and more than two terms                    
            //left site
            if (leftOperand.Contains("("))
            {
                leftOperand = RemoveParenthesis(leftOperand);
            }
            var leftTerms = GetLetterTerms(leftOperand);
            var dicLeftSmall = ConvertDictionaryUsingInterval(leftTerms, dicObj, false);
            var dicLeftLarge = ConvertDictionaryUsingInterval(leftTerms, dicObj, true);

            var leftNumSmall = Convert.ToDouble(Eval.Execute(leftOperand, dicLeftSmall));
            var leftNumBig = Convert.ToDouble(Eval.Execute(leftOperand, dicLeftLarge));
            (leftNumSmall, leftNumBig) = SwapSmaller(leftNumSmall, leftNumBig);

            //Right site
            if (rightOperand.Contains("("))
            {
                rightOperand = RemoveParenthesis(rightOperand);
            }
            var rightTerms = GetLetterTerms(rightOperand);
            var dicRightSmall = ConvertDictionaryUsingInterval(rightTerms, dicObj, false);
            var dicRightLarge = ConvertDictionaryUsingInterval(rightTerms, dicObj, true);

            var rightNumSmall = Convert.ToDouble(Eval.Execute(rightOperand, dicRightSmall));
            var rightNumBig = Convert.ToDouble(Eval.Execute(rightOperand, dicRightLarge));
            (rightNumSmall, rightNumBig) = SwapSmaller(rightNumSmall, rightNumBig);


            var isValid = (leftNumSmall <= rightNumBig && leftNumBig >= rightNumSmall);
            return isValid;
        }


        public static Dictionary<string, double> ConvertDictionaryUsingInterval(List<string> letters, Dictionary<string, ObjTerm> normalDic, bool isAddInterval)
        {
            var newDictionary = new Dictionary<string, double>();
            foreach (var letter in letters)
            {
                var signedNum = letter.Contains("-") ? -1.0 : 1.0;
                var newLetter = letter.Replace("-", "").Trim();
                var objItem = normalDic[newLetter];
                var power = objItem.decimals;
                var num = Convert.ToDouble(objItem.obj);
                var interval = Math.Pow(10, -power) / 2.0;

                //if it's a negative number, we need to make the number smaller to get the maximum interval
                var newNum = isAddInterval ? num + interval * signedNum : num - interval * signedNum;
                newDictionary.Add(newLetter, newNum);

            }

            return newDictionary;

        }


        public static string RemoveParenthesis(string expression)
        {
            //rename
            //remove parenthesis
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
            var list = GeneralUtils.GetRegexListOfMatchesWithCase(@"(-?\s*[XZ]\d{1,2})", expression);
            return list;
        }


    }
}
