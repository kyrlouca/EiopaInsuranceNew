using ConfigurationNs;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace XbrlReader
{

    class Program
    {
        static int Main(string[] args)
        {
#if DEBUG
            Console.WriteLine("XbrlReader in DEBUG MODE");            

            //var hydraQ = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\ValidationFiles\HYDRA Q1 2021.xbrl";
            //var CosmosQ=@"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\ValidationFiles\Cosmos Q1 2021.xbrl";
            //var SMuaeAnnual = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\ValidationFiles\SMUAE Annual 2020.xbrl";

            //var Euro260 = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Xbrl260\EuroLife Q4 2021.xbrl";
            //var Uni260 = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Xbrl260\Universal Q4 2021.xbrl";
            //var Cnp260 = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Xbrl260\CNP Asfalistiki Q4 2021.xbrl";
            //var med260 = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Xbrl260\MEDLIFE Q4 2021.xbrl";


            (var fundId, var filePath) = (102, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\Eurolife Q4_v1.xbrl");
            //(var fundId,var filePath) = (103,@"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\HD Q4 2021_v1.xbrl");
            //(var fundId, var filePath) = (101, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\HELLENIC ALICO Q4 2021_v1.xbrl");


            var xbrlDataTesting = new XbrlFileReader("IU260", 1, 99, fundId, "qrs", 2021, 4,filePath);            
                _ = new XbrlDataProcessor("IU260", xbrlDataTesting.DocumentId,xbrlDataTesting.FilingsSubmitted);
            

            Console.WriteLine("Finish");
            return 1;
#else        

            if (args.Length == 8)
            {


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
                Console.WriteLine($"XbrlReader v1: xbrlfile:{xbrlFile}");

                //no need for module (it is found in xbrl file)
                
                var xbrlData = new XbrlFileReader(solvencyVersion, currencyBatchId, userId, fundId,moduleCode, applicationYear, applicationQuarter, xbrlFile);          
                _ = new XbrlDataProcessor(solvencyVersion,  xbrlData.DocumentId,xbrlData.FilingsSubmitted);

                return 0;
            }
            else
            {

                var message = @".\XbrlReader  solvencyVersion currencyBatch userId fundId moduleCode year quarter filepath";
                Console.WriteLine(message);
                return 1;
            }

            return 1;
#endif
        }
    }
}
