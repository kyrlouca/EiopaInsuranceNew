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
            var currencyColIdx = -1;
            var rateColIdx = -1;
            var headerRowIdx = -1;
            fileName = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\curr2.xlsx";

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

            //*************************************************************
            // header row is the first non-empty line
            for (var i = 0; i <= sheet.LastRowNum; i++)
            {
                var row=sheet.GetRow(i);
                
                var isEmptyLine =  row is null || !row.Cells.Any(cell => cell is not null || !string.IsNullOrEmpty(cell?.ToString()));
                if (isEmptyLine)
                    continue;
                headerRowIdx = i;
                break;
            }
            if (headerRowIdx < 0)
            {
                return null;
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

            if(currencyColIdx<0 || rateColIdx < 0)
            {
                return null;
            }


            //*************************************************************
            //Read each currency - rate pair
            for (var i =  headerRowIdx+1; i <= sheet.LastRowNum; i++)
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
        public static int CurrencyBatchCreator(int year, int quarter, int wave)
        {
            var cb = new CurrencyBatch(year, quarter, wave);
            cb.ExchangeRates = ReadExcelFile("aa");
            var x = 3;
            return cb.ExchangeRates.Count;

        }
    }
}
