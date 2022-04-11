using System;
using HelperInsuranceFunctions;
namespace ExcelCreator
{
    class Program
    {

        public static int Main(string[] args)
        {

#if (DEBUG)


            Console.WriteLine("Excel Creator Debug mode");

            var (solvency,user,serial, file) = ("IU260",99,9765, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\xbrlAnnual\CypriaLife.xlsx");

            var efc = new ExcelFileCreator(solvency,user, serial, file);
            efc.CreateExcelFile();
            return 1;
#endif

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
            

        }

    }
}
