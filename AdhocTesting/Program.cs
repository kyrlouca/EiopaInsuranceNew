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
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            //var xx = SimplifiedExpression.RemoveOutsideParenthesis(@"abc+(efg)");
            //var xx = SimplifiedExpression.Create(1,null,@"X1>X2+3||X1=X3 && X4");
            //var xx = SimplifiedExpression.Process(1, null, @"X1>X2+3||(X1=X3 && X4)");
            //var x3 = SimplifiedExpression.ParseExpression(@"abcef", 0);

            var filename = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\xbrl4\test.xlsx";
            //ExcelValidationErrors.CreateErrorsExcelFile(9772, filename,"W");



            //var x4 = @"$c = $d - (-$e - $f + x2)";

            //[A-Z]{1,3}(\.\d\d){4}
            //
            var xx = ModifyTableCode("S.19.01.01.05");
            var vv = 3;

        }
        static bool OddTableCodeSelector(string tableCode)
        {
            //retruns true if last part of tablecode is odd // "S.19.01.01.05"=> true because "05" is odd
            var match = RegexConstants.TableCodeRegExP.Match(tableCode);
            if (match.Success)
            {
                // "S.19.01.01.05"=> "05"
                var lastDigits = match.Groups[2].Captures
                    .Select(cpt => cpt.Value.Substring(1))
                    .ToArray()[3];

                return int.Parse(lastDigits) % 2 != 0;

            }
            return false;
        }


        static string ModifyTableCode(string tableCode)
        {
            //retruns true if last part of tablecode is odd // "S.19.01.01.05"=> true because "05" is odd
            var match = RegexConstants.TableCodeRegExP.Match(tableCode);
            if (match.Success)
            {
                // "S.19.01.01.05"=> "05"
                var lastDigits = match.Groups[2].Captures
                    .Select(cpt => cpt.Value.Substring(1))
                    .ToArray();

                var incDigit = int.Parse(lastDigits[3])+1;
                var modCode= $"{match.Groups[1].Value}.{lastDigits[0]}.{lastDigits[1]}.{lastDigits[2]}.{incDigit:D2}";
                var x = 3;
            }
            return tableCode;
        }



    }
}
