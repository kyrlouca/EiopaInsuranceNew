using System;
using System.IO;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace ExcelCreatorV
{

    public class ERROR_Rule
    {
        public int ErrorId { get; set; }
        public int RuleId { get; set; }        
        public string Scope { get; set; }
        public string RowCol { get; set; }        
        public string RuleMessage { get; set; }        
        public string TableBaseFormula { get; set; }
        public string Filter { get; set; }        
        public string SheetCode { get; set; }
        public string DataValue { get; set; }
        public bool IsDataError { get; set; }
        public bool IsWarning { get; set; }
        public bool IsError { get; set; }
    }

    public static class ExcelValidationErrors
    {        
        static public bool CreateErrorsExcelFile(int documentId, string filePath, string reportType="B")
        {
            const string InsuranceDatabaseConnectionString = "Data Source = KYR-RYZEN\\SQLEXPRESS ; Initial Catalog =InsuranceDatabase; Integrated Security = true; TrustServerCertificate=True;";
            using var connectionEiopa = new SqlConnection(InsuranceDatabaseConnectionString);

            var excelBook = new XSSFWorkbook();
            var excelSheet = excelBook.CreateSheet("Errors");

            var titleStyle = CreateTitleStyle(excelBook);
            var dataStyle = CreateDataStyle(excelBook);

            var errorFields = typeof(ERROR_Rule).GetProperties().ToList();

            var sqlErrors = @"
                    SELECT  Er.ErrorId
                           ,Er.RuleId
                           ,Er.Scope
                           ,Er.rowCol
                           ,Er.TableBaseFormula
                           ,Er.Filter
                           ,Er.RuleMessage
                           ,Er.DataValue     
                           ,Er.SheetCode                           
                           ,Er.IsDataError
                           ,Er.IsWarning
                           ,Er.IsError                           
                    FROM dbo.ERROR_Rule Er
                    WHERE Er.ErrorDocumentId = @documentId
                    ORDER BY er.RuleId
                           ,Er.Scope
                           ,Er.rowCol
            ";
            var errors = connectionEiopa.Query<ERROR_Rule>(sqlErrors, new { documentId }).ToList();

            //filter out errors or warnings
            if (reportType == "E")
            {
                errorFields = errorFields.Where(ff => ff.Name != "IsWarning").ToList();
                errors = errors.Where(er => er.IsError || er.IsDataError).ToList();
            }else if (reportType == "W")
            {
                errors = errors.Where(er => er.IsWarning).ToList();
                errorFields = errorFields.Where(ff => ff.Name != "IsError").ToList();
            }

            //create titles
            var titleRow = excelSheet.CreateRow(0);
            for (var i = 0; i < errorFields.Count; i++)
            {                
                var titleCell = titleRow.CreateCell(i);
                titleCell.CellStyle = titleStyle;
                titleCell.SetCellValue(errorFields[i].Name);
            }
           
            var rowIdx = 1;            
            foreach (var error in errors)
            {
                var dataRow = excelSheet.CreateRow(rowIdx);                

                var colIdx = 0;
                foreach (var errorField in errorFields)
                {
                    var cell = dataRow.CreateCell(colIdx);

                    var errorFieldType = errorField.GetValue(error)?.GetType();
                    if (errorFieldType is null)
                    {
                        cell.SetCellValue("");
                    }
                    else if (errorFieldType == typeof(int) || errorFieldType == typeof(bool))
                    {
                        var val = Convert.ToInt32(errorField.GetValue(error));
                        cell.SetCellValue(val);
                        excelSheet.SetColumnWidth(colIdx, 2000);
                    }
                    else
                    {
                        cell.SetCellValue(errorField.GetValue(error).ToString());
                        excelSheet.SetColumnWidth(colIdx, 5000);
                    }
                    colIdx += 1;
                }
                rowIdx += 1;
            }

            SaveWorkbook(excelBook, filePath);
            return true;
        }

        static public void SaveWorkbook(IWorkbook workbook, string path)
        {
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            try
            {
                workbook.Write(fileStream,false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static public ICellStyle CreateTitleStyle(XSSFWorkbook workBook)
        {
            var style = workBook.CreateCellStyle();
            var font = workBook.CreateFont();
            font.FontHeightInPoints = 12;
            font.FontName = "Calibri";
            font.IsBold = true;
            style.SetFont(font);
            return style;
        }

        static public ICellStyle CreateDataStyle(XSSFWorkbook workBook)
        {
            var style = workBook.CreateCellStyle();
            var font = workBook.CreateFont();
            font.FontHeightInPoints = 10;
            font.FontName = "Calibri";
            font.IsBold = false;
            style.SetFont(font);
            return style;

        }

    }
}
