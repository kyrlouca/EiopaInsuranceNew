using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NPOI;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace CurrencyRates
{
    public class ExchangeRate
    {
        public ExchangeRate(string currencyCode, double rate)
        {
            CurrencyCode = currencyCode;
            Rate = rate;
        }

        string CurrencyCode { get; set; }
        double Rate { get; set; }
    }

    internal class CurrencyBatch
    {
        public int Year { get; set; }
        public int Quarter { get; set; }
        public int Wave { get; set; }
        public List<ExchangeRate> ExchangeRates { get; set; }

        private CurrencyBatch(int year, int quarter, int wave)
        {
            Year = year;
            Quarter = quarter;
            Wave = wave;
        }

        public static List<ExchangeRate>? ReadExcelFile(string fileName)
        {
            var currencyColumn = -1;
            var rateColumn = -1;
            fileName = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\Currencies.xlsx";

            var rates = new List<ExchangeRate>();
            ISheet sheet;
            using var stream = new FileStream(fileName, FileMode.Open);
            stream.Position = 0;
            XSSFWorkbook excelFile;

            try
            {
                excelFile = new XSSFWorkbook(stream);
            }
            catch (FileNotFoundException fnf)
            {
                Console.WriteLine(fnf.Message);
                return null;
            }
            
            
            sheet = excelFile.GetSheetAt(0);
            var headerRow = sheet.GetRow(0);
            int cellCount = headerRow.LastCellNum;
            for (int j = 0; j < cellCount; j++)
            {
                var cell = headerRow.GetCell(j);
                var cellText = cell?.ToString()?.Trim()?.ToUpper() ?? "";
                if (cell == null || string.IsNullOrWhiteSpace(cell.ToString()))
                    continue;
                if (string.IsNullOrEmpty(cellText))
                    continue;

                if (cellText == "CURRENCY")
                {
                    currencyColumn = j;
                }
                else if (cellText == "EXCHANGERATE")
                {
                    rateColumn = j;
                }

            }

            for (var i = (sheet.FirstRowNum + 1); i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                var currency = row.GetCell(currencyColumn)?.ToString() ?? "";
                var rate = row.GetCell(rateColumn)?.NumericCellValue ?? -1.0;
                if (!string.IsNullOrEmpty(currency) && rate != -1)
                {
                    rates.Add(new ExchangeRate(currency, rate));
                }
            }

            return rates;
        }
        public static int CurrencyBatchCreator(int year, int quarter, int wave)
        {
            var cb = new CurrencyBatch(year, quarter, wave);
            cb.ExchangeRates = ReadExcelFile("aa");
            var x = 3;
            return cb.ExchangeRates.Count;

        }
    }
}
