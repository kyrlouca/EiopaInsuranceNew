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
#if (DEBUG)
            Console.WriteLine("XbrlReader in DEBUG MODE");            

            if (1 == 2)
            {


                var annualFilings = new List<string>() { "S.19.01" };
                var filings = new List<string>() {
                                        "S.01.01",
                                        "S.01.02",
                                        "S.02.01",
                                        "S.05.01",
                                        "S.05.01",
                                        "S.06.02",
                                        "S.06.02",
                                        "S.12.01",
                                        "S.17.01",
                                        "S.23.01",
                                        "S.23.01",
                                        "S.28.02",
                                        "S.28.02",
                                        "S.28.02",
                                        "S.28.02",
                                        "S.28.02",
                                        "S.28.02"
                                        };
                _ = new XbrlDataProcessor("IU250", 8648, annualFilings);
                return 1;



                var config = Configuration.GetInstance("IU250").Data;               
                using var connectionEiopa = new SqlConnection(config.EiopaDatabaseConnectionString);

                //var sqlsel = "select tab.TableID, tab.ZDimVal,YDimVal from mTable tab where tab.TableID=124";
                //var factSig = "MET(s2md_met:mi346)|s2c_dim:VG(s2c_AM:x80)";
                //var table = connectionEiopa.QueryFirst<MTable>(sqlsel);
                //var xx = XbrlDataProcessor.FindMatchingFactsV3(config, 8549, factSig);
                //MET(s2md_met:di1037)|s2c_dim:BL(s2c_LB:x141)|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(ID:FAC_594290)|s2c_dim:RD(ID:P_594290)|s2c_dim:RE(ID:RE_FIRE)

                //var xx = XbrlDataProcessor.IsFactDimMatchingCell(config, factDim, cellDim);

                var factSig = @"MET(s2md_met:mi1104)|s2c_dim:BL(s2c_LB:x136)|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(ID:FAC_MON/089/14)|s2c_dim:RD(ID:P_MON/089/14)|s2c_dim:RE(ID:RE_PERSONAL_ACCIDENT)";
                var cellSig = @"MET(s2md_met:mi1104)|s2c_dim:BL(*[3343;1512;0])|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(*)|s2c_dim:RD(*)|s2c_dim:RE(*)";
                var xx = XbrlDataProcessor.IsFactSignatureMatchingExpensive(config, cellSig, factSig);

                //var cellFSig="MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(*?[237])|s2c_dim:RM(s2c_TI:x49)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";

                //var xxx = XbrlDataProcessor.FindMatchingFactsV5(config, 8626, cellFSig);

                return 1;
            }

            //var simpleTest= @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Trimmed.xbrl";
            //var ancoriaQrs = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Ancoria Insurance - QRS Q2 2021.xbrl";
            //var graw2020 = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-25 0\GraweRe - Annual 2020.xbrl"; //6051
            var defenceAnnual = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\UK Defence - Annual 2020.xbrl";
            //var simpleFile = @"C:\Users\kyrlo\soft\dotnet\insuranc e-project\testing-250\simple.xbrl"; //6051

            var hydraQ = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\ValidationFiles\HYDRA Q1 2021.xbrl";
            var CosmosQ=@"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\ValidationFiles\Cosmos Q1 2021.xbrl";
            var SMuaeAnnual = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\ValidationFiles\SMUAE Annual 2020.xbrl";

            //var Euro260 = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Xbrl260\EuroLife Q4 2021.xbrl";
            //var Uni260 = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Xbrl260\Universal Q4 2021.xbrl";
            //var Cnp260 = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Xbrl260\CNP Asfalistiki Q4 2021.xbrl";
            //var med260 = @"C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Xbrl260\MEDLIFE Q4 2021.xbrl";

            var Euro260E = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\Eurolife Q4_v1.xbrl";
            var hell = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\HELLENIC ALICO Q4 2021_v1.xbrl";

            var xbrlDataTesting = new XbrlFileReader("IU260", 1, 99, 101, "qrs", 2021, 4,hell);            
            _ = new XbrlDataProcessor("IU260", xbrlDataTesting.DocumentId,xbrlDataTesting.FilingsSubmitted);


            //var sig = @"MET(s2md_met:mi503)|s2c_dim:BL(s2c_LB:x10)|s2c_dim:DI(s2c_DI:x5)|s2c_dim:IZ(s2c_RT:x1)|s2c_dim:TB(s2c_LB:x28)|s2c_dim:VG(s2c_AM:x84)";
            //var tableId = 61;
            //var result= DatabaseWriter.FindFactRowCol(tableId, sig);

            Console.WriteLine("Finish");
            return 1;
#else        

            if (args.Length == 8)
            {


                //C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Universal Life Insurance Public Company Limited Q3 2021.xbrl
                //.\XbrlReader.exe "IU250" 1 1 1 2021 0 "C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Altius Insurance - Annual 2020.xbrl"
                //.\XbrlReader.exe "IU250" 1 1 1 2021 3 "C:\Users\kyrlo\soft\dotnet\insurance-project\testing-250\Universal Life Insurance Public Company Limited Q3 2021.xbrl"
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

                var message = @".\XbrlReader  solvencyVersion currencyBatch userId fundId year quarter filepath";
                Console.WriteLine(message);
                return 1;
            }

            return 1;
#endif
        }
    }
}
