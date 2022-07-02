// See https://aka.ms/new-console-template for more information



#if (DEBUG)
    Console.WriteLine("Currency Batch Debug mode");

    var fileName = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\curr2.xlsx";
    var xx = CurrencyRates.CurrencyBatch.CreateCurrenciesFromFile(fileName, 2022, 0, 1);    
    return 1;
#endif

if (args.Length == 4)
{
    //.\CurrencyRates "C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\curr2.xlsx" 2021 0 1
    var excelFileName = args[0].Trim();
    var year = int.TryParse(args[1], out var arg1) ? arg1 : 0;
    var quarter = int.TryParse(args[2], out var arg2) ? arg2 : 0;
    var wave = int.TryParse(args[3], out var arg3) ? arg3 : 0;
    Console.WriteLine($"Currency Batch=> {excelFileName} year:{year} quarter:{quarter} wave:{wave}");
    var xyz= CurrencyRates.CurrencyBatch.CreateCurrenciesFromFile(excelFileName, year, quarter, wave);    
    return 1;
}
else
{
    var message = @".\CurrencyRates excelFileName, year, quarter, wave";
    Console.WriteLine(message);
    return 0;
}

return 1;


