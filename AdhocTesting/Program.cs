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

            

            

        }
        


    }
}
