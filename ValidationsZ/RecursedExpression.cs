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

    public class xxTermExpression
    {
        public string Letter { get; set; }
        public string TermExpressionStr { get; set; }
        public bool IsValid { get; set; }
    }

    public class xxSimplifiedExpression
    {
        public int RuleId { get; set; }
        public string Expression { get; set; }
        public string SymbolExpressionFinal { get; set; } = "";
        public Dictionary<string, ObjTerm> ObjTerms { get; set; } = new();
        public Dictionary<string, object> PlainObjTerms { get; set; } = new();
        public bool IsValid { get; set; }
        public List<TermExpression> TermExpressions { get; set; } = new();
        public Dictionary<string, SimplifiedExpression> PartialSimplifiedExpressions { get; set; } = new();
        private xxSimplifiedExpression() { }





        public static xxSimplifiedExpression CreateExpression(string expression)
        {
            return null;
        }

        public static (string selection, int newStart) ParseExpression(string expresssion, int start)
        {

            var max = expresssion.Length;

            var ind = expresssion.IndexOfAny(new char[] { '&', '|', '(' }, start);
            if (ind < 0)
            {
                return ("", -1);
            }
            var selection = expresssion[start..ind];
            var newStart = expresssion[ind] == '(' ? ind + 1 : ind + 2;

            return (selection, newStart);

        }

        private xxSimplifiedExpression(string expression)
        {
            Expression = xxRemoveOutsideParenthesis(expression);
        }



        public List<TermExpression> CreateTermExpressions()
        {
            var partialExpressions = new List<TermExpression>();
            if (string.IsNullOrWhiteSpace(SymbolExpressionFinal))
                return partialExpressions;

            var terms = SymbolExpressionFinal.Split(new string[] { "&&", "||" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var count = 0;
            foreach (var term in terms)
            {
                partialExpressions.Add(new TermExpression() { LetterId = $"VV{count}", TermExpressionStr = term.Trim() });
                count += 1;
            }
            return partialExpressions;
        }



        static (bool isValid, string leftOperand, string operatorUsed, string rightOperand) xxSplitAlgebraExpresssionNew(string expression)
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

        private static Dictionary<string, ObjTerm> xxCreateObjectTerms(List<RuleTerm> ruleTerms)
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



        public static Dictionary<string, double> xxConvertDictionaryUsingInterval(List<string> letters, Dictionary<string, ObjTerm> normalDic, bool isAddInterval)
        {
            var newDictionary = new Dictionary<string, double>();
            foreach (var letter in letters)
            {
                var signedNum = letter.Contains("-") ? -1.0 : 1.0;
                var newLetter = letter.Replace("-", "").Trim();
                var objItem = normalDic[newLetter];
                var power = objItem.decimals;


                try
                {
                    var num = Convert.ToDouble(objItem.obj);
                    var interval = Math.Pow(10, -power) / 2.0;

                    //if it's a negative number, we need to make the number smaller to get the maximum interval
                    var newNum = isAddInterval ? num + interval * signedNum : num - interval * signedNum;
                    newDictionary.Add(newLetter, newNum);
                }
                catch
                {
                    newDictionary.Add(newLetter, 0);
                }


            }

            return newDictionary;

        }


        public static string xxExpressionWithoutParenthesis(string expression)
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


        public static (double, double) xxSwapSmaller(double a, double b)
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


        public static List<string> xxGetLetterTerms(string expression)
        {
            //it will return the letter terms but with the MINUS sign in front
            var list = GeneralUtils.GetRegexListOfMatchesWithCase(@"(-?\s*[XZ]\d{1,2})", expression);
            return list;
        }

        public static string xxRemoveOutsideParenthesis(string expression)
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
