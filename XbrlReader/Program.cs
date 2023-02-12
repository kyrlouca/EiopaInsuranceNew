using ConfigurationNs;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace XbrlReader
{
    internal class Program
    {
        static int Main(string[] args)
        {
            var debug = false;
#if DEBUG
            debug = true;
#endif
            if (debug)
            {
                Console.WriteLine("XbrlReader in DEBUG MODE");

                //C:\Users\kyrlo\soft\dotnet\pension-project\Pension_dev_NEW\Testing\Testing270\xbrlFiles\xbrl1.xbrl
                //(var fundIdT, var filePath) = (42, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\Universal.xbrl");
                //(var fundIdT, var filePath) = (177, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\HD.xbrl");


                (var fundIdT, var filePath) = (42, @"C:\Users\kyrlo\soft\dotnet\pension-project\Pension_dev_NEW\Testing\Testing270\xbrlFiles\xbrl1.xbrl");
                XbrlFileReader.StarterStatic("PU270", 1, 2, fundIdT, "qri", 2022, 4, filePath);

                Console.WriteLine("Finish");
                return 1;
            }



            if (args.Length == 8)
            {
                //user =1 does not check for validation dates

                //
                //.\XbrlReader.exe "IU270" 1 1 42 "qrs" 2022 3 "C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\Universal.xbrl"                
                var solvencyVersion = args[0].Trim();
                var currencyBatchId = int.TryParse(args[1], out var arg1) ? arg1 : 0;
                var userId = int.TryParse(args[2], out var arg2) ? arg2 : 0;
                var fundId = int.TryParse(args[3], out var arg3) ? arg3 : 0;
                var moduleCode = args[4];
                var applicationYear = int.TryParse(args[5], out var arg5) ? arg5 : 0;
                var applicationQuarter = int.TryParse(args[6], out var arg6) ? arg6 : 0;
                var xbrlFile = args[7];
                Console.WriteLine($"XbrlReader v1.001: xbrlfile:{xbrlFile}");

                XbrlFileReader.StarterStatic(solvencyVersion, currencyBatchId, userId, fundId, moduleCode, applicationYear, applicationQuarter, xbrlFile);
                return 0;
            }
            else
            {

                var message = @".\XbrlReader  solvencyVersion currencyBatch userId fundId moduleCode year quarter filepath";
                Console.WriteLine(message);
                return 1;
            }

            return 1;
        }
    }
}
