// See https://aka.ms/new-console-template for more information
using ExcelCreatorV;

Console.WriteLine("Hello, ExcelV!");
var isDebug = false;
#if DEBUG
isDebug = true;
#endif

if (isDebug)
{
    Console.WriteLine("Excel in debug2");
    var fl = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\HD Annual 2022.xlsx";
    //var (solvencyD, userD, serialD, fileD) = ("IU270", 99, 12977, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\HD Annual 2022.xlsx");
    var (solvencyD, userD, serialD, fileD) = ("IU270", 99, 12987, @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\xxxx.xlsx");
    //ExcelFileCreator.StaticStartCreateTheExcelFile(solvencyD, userD, serialD, fileD);
    ExcelFileCreator.StaticStartCreateTheExcelFile(solvencyD, 172, "qrs", 2023,1, fileD);
    return 0;
}


Console.WriteLine("ExcelV outside debug");
if (args.Length == 4)
{
    //.\ExcelCreator.exe "IU270" 99 12972 "C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\universal22.xlsx"                
    var solvencyVersion = args[0].Trim();
    var userId = int.TryParse(args[1], out var arg1) ? arg1 : 0;
    //var documentId = int.TryParse(args[2], out var arg2) ? arg2 : 0;
    int.TryParse(args[2], out var documentId);
    var fileName = args[3].Trim();


    Console.WriteLine($"Started ExcelCreator=> Solvency:{solvencyVersion}  userId:{userId} docId:{documentId} fileName:{fileName}");
    ExcelFileCreator.StaticStartCreateTheExcelFile(solvencyVersion, userId, documentId, fileName);

    return 1;
}
else if (args.Length == 6)
{
    //.\ExcelCreator.exe "IU270" 173  "qrs" 2023 1 "C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\TEST.xlsx"
    var solvencyVersion = args[0].Trim();
    var fundId = int.TryParse(args[1], out var arg1) ? arg1 : 0;
    var moduleCode = args[2].Trim();
    var applicationYear = int.TryParse(args[3], out var arg3) ? arg3 : 0;    
    var applicationQuarter = int.TryParse(args[4], out var arg4) ? arg4 : 0;    
    var fileName = args[5].Trim();


    Console.WriteLine($"Started ExcelCreator=> Solvency:{solvencyVersion}  userId:{fundId} module:{moduleCode} year:{applicationYear} quarter:{applicationQuarter} fileName:{fileName}");
    ExcelFileCreator.StaticStartCreateTheExcelFile(solvencyVersion, fundId,moduleCode, applicationYear, applicationQuarter, fileName);

    return 1;
}
else
{

    var message = @" Incorrect number of Arguments Use => solvencyVersion userId documentId filename";
    Console.WriteLine(message);
    return 0;
}

return 1;


