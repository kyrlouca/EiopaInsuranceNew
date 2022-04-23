using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NPOI;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using ConfigurationNs;
using Microsoft.Data.SqlClient;
using Dapper;

namespace CurrencyRates
{
    public class ExchangeRate
    {
        public ExchangeRate(string currencyCode, double rate)
        {
            CurrencyCode = currencyCode;
            Rate = rate;
        }

        public string CurrencyCode { get; set; }
        public double Rate { get; set; }
    }

    internal class CurrencyBatch
    {

        public static List<ExchangeRate> ReadExcelFile(string fileName)
        {
            var currencyColIdx = -1;
            var rateColIdx = -1;
            var headerRowIdx = -1;
            var rates = new List<ExchangeRate>();
            //fileName = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\curr2.xlsx";
            if (string.IsNullOrEmpty(fileName))
            {
                return rates;
            }

            
            ISheet sheet;
            XSSFWorkbook excelFile;
            try
            {
                using var stream = new FileStream(fileName, FileMode.Open);
                stream.Position = 0;
                excelFile = new XSSFWorkbook(stream);
            }
            catch (FileNotFoundException fnf)
            {
                Console.WriteLine(fnf.Message);
                return rates;
            }
            catch (IOException e)
            {
                Console.WriteLine($"The file could not be opened: '{e}'");
                return rates;
            }


            sheet = excelFile.GetSheetAt(0);

            //*************************************************************
            // header row is the first non-empty line
            for (var i = 0; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);

                var isEmptyLine = row is null || !row.Cells.Any(cell => cell is not null || !string.IsNullOrEmpty(cell?.ToString()));
                if (isEmptyLine)
                    continue;
                headerRowIdx = i;
                break;
            }
            if (headerRowIdx < 0)
            {
                return rates;
            }

            //*************************************************************
            //check if the header row has Currency and ExchangeRate titles
            var headerRow = sheet.GetRow(headerRowIdx);
            int cellCount = headerRow.LastCellNum;
            for (var j = 0; j < cellCount; j++)
            {
                var cell = headerRow.GetCell(j);
                var cellText = cell?.ToString()?.Trim()?.ToUpper() ?? "";
                if (cell == null || string.IsNullOrWhiteSpace(cell.ToString()))
                    continue;
                if (string.IsNullOrEmpty(cellText))
                    continue;

                if (cellText == "CURRENCY")
                {
                    currencyColIdx = j;
                }
                else if (cellText == "EXCHANGERATE")
                {
                    rateColIdx = j;
                }

            }

            if (currencyColIdx < 0 || rateColIdx < 0)
            {
                return rates;
            }


            //*************************************************************
            //Read each currency - rate pair
            for (var i = headerRowIdx + 1; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                var currency = row.GetCell(currencyColIdx)?.ToString() ?? "";
                var rate = row.GetCell(rateColIdx)?.NumericCellValue ?? -1.0;
                if (!string.IsNullOrEmpty(currency) && rate != -1)
                {
                    rates.Add(new ExchangeRate(currency, rate));
                }
            }

            return rates;
        }

        public static void SaveData(int year, int quarter, int wave, List<ExchangeRate> rates)
        {
            var configObject = Configuration.GetInstance("IU260").Data;
            using var connectionLocal = new SqlConnection(configObject.LocalDatabaseConnectionString);
            var isValid = true;

            var sqlDel = @"delete from CurrencyBatch where Year = @year and Quarter = @quarter and Wave = @wave";
            connectionLocal.Execute(sqlDel, new { year, quarter, wave });

            var sqlInsertBatch = @"
                INSERT INTO dbo.CurrencyBatch (DateCreated, Year, Quarter, Wave ,status)  VALUES (@DateCreated, @Year, @Quarter, @Wave, @Status);
                SELECT CAST(SCOPE_IDENTITY() as int);
            ";

            var currencyBatchId = connectionLocal.QuerySingleOrDefault<int>(sqlInsertBatch, new { dateCreated = DateTime.Now, year, quarter, wave, status="E" });
            if (currencyBatchId == 0) return;
            var count = 0;
            foreach (var rate in rates)
            {
                var sqlInsertRate = @"INSERT INTO dbo.CurrencyExchangeRate (CurrencyBatchId, Currency, ExchangeRate) VALUES (@currencyBatchId, @currency, @exchangeRate)";
                try
                {
                    connectionLocal.Execute(sqlInsertRate, new { currencyBatchId, currency = rate.CurrencyCode, exchangeRate = rate.Rate });
                    count++; 
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    isValid= false;
                }
            }
            
            var finalStatus = count > 0 && isValid ? "S" : "E";
            var sqlUpdate = @"update CurrencyBatch set status = @status where currencyBatchId=@currencyBatchId";
            connectionLocal.Execute(sqlUpdate, new { currencyBatchId,status=finalStatus});
        }

        public static int CurrencyBatchCreator(string filename, int year, int quarter, int wave)
        {
            var rates = ReadExcelFile(filename);
            SaveData(year, quarter, wave, rates);
            return rates.Count;

        }
    }
}
