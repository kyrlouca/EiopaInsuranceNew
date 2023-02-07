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
#if DEBUG
            Console.WriteLine("XbrlReader in DEBUG MODE");



            //C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\final\London P and I Annual 2021 v1.xbrl
            //

            //C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\final\ALTIUS INSURANCE LTD Q1 2022.xbrl
            //C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\Universal.xbrl
            (var fundIdT, var filePath) = (44, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\Universal.xbrl");
            //XbrlFileReader.ProcessXbrlFile("IU260", 1, 2, fundIdT, "ars", 2021, 0,filePath);
            XbrlGenerator.GenerateXbrlFile("IU270", 1, 2, fundIdT, "qrs", 2022, 4, filePath);

            Console.WriteLine("Finish");
            return 1;

#endif


            if (args.Length == 8)
            {
                //user =1 does not check for validation dates

                //C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Universal Life Insurance Public Company Limited Q3 2021.xbrl
                //.\XbrlReader.exe "IU260" 1 1 1 "qrs" 2021 0 "C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Altius Insurance - Annual 2020.xbrl"                
                var solvencyVersion = args[0].Trim();
                var currencyBatchId = int.TryParse(args[1], out var arg1) ? arg1 : 0;
                var userId = int.TryParse(args[2], out var arg2) ? arg2 : 0;
                var fundId = int.TryParse(args[3], out var arg3) ? arg3 : 0;
                var moduleCode = args[4];
                var applicationYear = int.TryParse(args[5], out var arg5) ? arg5 : 0;
                var applicationQuarter = int.TryParse(args[6], out var arg6) ? arg6 : 0;
                var xbrlFile = args[7];
                Console.WriteLine($"XbrlReader v1.001: xbrlfile:{xbrlFile}");
                                
                //XbrlFileReader.ProcessXbrlFile(solvencyVersion, currencyBatchId, userId, fundId,moduleCode, applicationYear, applicationQuarter, xbrlFile);
                XbrlGenerator.GenerateXbrlFile(solvencyVersion, currencyBatchId, userId, fundId, moduleCode, applicationYear, applicationQuarter, xbrlFile);
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
