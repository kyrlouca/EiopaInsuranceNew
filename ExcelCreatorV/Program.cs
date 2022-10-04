// See https://aka.ms/new-console-template for more information
using ExcelCreatorV;

Console.WriteLine("Hello, ExcelV!");
#if DEBUG

Console.WriteLine("Excel in debug2");
var (solvencyD, userD, serialD, fileD) = ("IU260", 99, 12957, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\newVersion\atlantic2021.xlsx");

ExcelFileCreator.CreateTheExcelFile(solvencyD, userD, serialD, fileD);
return 0;
#endif

Console.WriteLine("ExcelV outside debug");
if (args.Length == 4)
{
    //.\ExcelCreator "IU260" 99 12886 "C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\final\crashxx.xlsx"

    var solvencyVersion = args[0].Trim();
    var userId = int.TryParse(args[1], out var arg1) ? arg1 : 0;
    //var documentId = int.TryParse(args[2], out var arg2) ? arg2 : 0;
    int.TryParse(args[2], out var documentId);
    var fileName = args[3].Trim();

    Console.WriteLine($"Started ExcelCreator=> Solvency:{solvencyVersion}  userId:{userId} docId:{documentId} fileName:{fileName}");
    ExcelFileCreator.CreateTheExcelFile(solvencyVersion, userId, documentId, fileName);

    return 1;
}
else
{

    var message = @" Incorrect number of Arguments Use => solvencyVersion userId documentId filename";
    Console.WriteLine(message);
    return 0;
}

return 1;


