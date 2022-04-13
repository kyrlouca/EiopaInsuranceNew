using System;
using GeneralUtilsNs;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Validations;
using EiopaConstants;
using ExcelCreator;

namespace AdhocTesting
{
    public class Program
    {
        
        
        enum Fts{ exp,count,empty,isfallback,min,max,sum,matches,ftdv,ExDimVal };
        static void Main(string[] args)
        {

            //var xx = SimplifiedExpression.RemoveOutsideParenthesis(@"abc+(efg)");
            //var xx = SimplifiedExpression.Create(1,null,@"X1>X2+3||X1=X3 && X4");
            var xx = SimplifiedExpression.Create(1, null, @"X1>X2+3||(X1=X3 && X4)");
            //var x3 = SimplifiedExpression.ParseExpression(@"abcef", 0);

            var filename = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\xbrl4\test.xlsx";

            


            var x4 = @"$c = $d - (-$e - $f + x2)";

            

            

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
