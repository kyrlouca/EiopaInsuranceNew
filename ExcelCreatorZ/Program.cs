using System;
using HelperInsuranceFunctions;
namespace ExcelCreatorNs
{
    class Program
    {

        public static int Main(string[] args)
        {
#if (DEBUG)


            Console.WriteLine("Excel Creator Debug mode");            
            
            var (serial, file) = (9712, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\ExcelCreated\HellenikV3.xlsx");

            var efc = new ExcelFileCreator("IU260", 99, serial, file);
            efc.CreateExcelFile();
            return 1;
#else
            if (args.Length == 4)
            {
                //.\ExcelCreator "IU260" 99 8685 "C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\ExcelCreated\UniversalQ4.xlsx"
                                
                var solvencyVersion = args[0].Trim();
                var userId = int.TryParse(args[1], out var arg1) ? arg1 : 0;
                var documentId = int.TryParse(args[2], out var arg2) ? arg2 : 0;
                var fileName = args[3];

                var xlsCreator = new ExcelFileCreator(solvencyVersion, userId, documentId, fileName);
                xlsCreator.CreateExcelFile();


                return 1;
            }
            else
            {

                var message = @"ExcelCreator solvencyVersion userId documentId filename";
                Console.WriteLine(message);
                return 0;
            }

            return 1;

        }
#endif
        }
    }
}


    
