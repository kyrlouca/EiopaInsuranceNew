// See https://aka.ms/new-console-template for more information
using ExcelCreatorV;

Console.WriteLine("Hello, ExcelV!");
#if DEBUG

Console.WriteLine("Excel in debug2");
var (solvencyD, userD, serialD, fileD) = ("IU260", 99, 11824, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\ExcelCreated\AmericanSteam.xlsx");
var efc = new ExcelFileCreator(solvencyD, userD, serialD, fileD);
efc.CreateExcelFile();

#else
    Console.WriteLine("ExcelV outside debug");
    if (args.Length == 4)
            {
                //.\ExcelCreator "IU260" 99 8685 "C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\ExcelCreated\UniversalQ4.xlsx"
                 
                var solvencyVersion = args[0].Trim();
                var userId = int.TryParse(args[1], out var arg1) ? arg1 : 0;
                var documentId = int.TryParse(args[2], out var arg2) ? arg2 : 0;
                var fileName = args[3];

                Console.WriteLine($"before userId:{userId} docId:{documentId} fileName:{fileName}");
                var xlsCreator = new ExcelFileCreator(solvencyVersion, userId, documentId, fileName);
                Console.WriteLine($"Before create. userId:{userId} docId:{documentId} fileName:{fileName}");
                xlsCreator.CreateExcelFile();


                return 1;
            }
            else
            {

                var message = @"ExcelCreator solvencyVersion userId documentId filename";
                Console.WriteLine(message);
                return 0;
            }
#endif
return 1;

