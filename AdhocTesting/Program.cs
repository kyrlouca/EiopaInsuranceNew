using System;
using GeneralUtilsNs;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Validations;
using EiopaConstants;

namespace AdhocTesting
{
    public class Program
    {
        enum Fts{ exp,count,empty,isfallback,min,max,sum,matches,ftdv,ExDimVal };
        static void Main(string[] args)
        {
            //var sdfasfd = Enum.GetValues
            var x1 = @"$c = $d - (-$e + $f)";
            var x2 = @"$c = $d - ($e - $f + x2)";
            var x3 = @"$c = $d - (+$e - $f + x2)";
            var x4 = @"$c = $d - (-$e - $f + x2)";

            var x = RuleStructure.RemoveParenthesis(x4);

            var exp= "X0 <= 0.2 * (X0 + X1) ";

            var xx = parseExp(exp);

            var xxx = "";

        }

        static (string leftOp,string Op, string rightOp) parseExp(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                    return ("", "", "");
            var reg = @"(.*)\s*([<>=]=)\s*(.*)";
            var parts = GeneralUtils.GetRegexSingleMatchManyGroups(reg, expression);
            
            if (parts.Count == 4)
            {
                var left = parts[1];
                var op = parts[2];
                var right = parts[3];
                return (left, op, right);
            }
            return ("", "", "");        

        }


    }
}
