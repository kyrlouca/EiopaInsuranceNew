using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelCreatorV;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using ConfigurationNs;
using EntityClasses;
using Microsoft.Data.SqlClient;
using Dapper;

namespace ExcelCreatorV
{
    readonly record struct SheetRecord
    {
        //public ISheet Sheet { get; init; }
        public string TabSheetName { get; init; }
        public string Description { get; init; }
        public SheetRecord(string tabSheetName, string description)
        {
            TabSheetName = tabSheetName;
            Description = description;            
        }
    }
    internal class IndexSheetList
    {
        ConfigObject? ConfObject { get; set; }
        XSSFWorkbook ExcelBook { get; set; }
        WorkbookStyles WorkbookStyles { get; set; } 
        public ISheet IndexSheet { get; internal set; }
        public string SheetName { get; init; }
        public string SheetDescription { get; init; }
        List<SheetRecord> SheetRecords { get; set; } = new List<SheetRecord>();
        public IndexSheetList(ConfigObject confObject, XSSFWorkbook excelBook, WorkbookStyles workbookStyles, List<TemplateSheetInstance> dBsheets,string sheetName, string sheetDescription)
        {
            ConfObject = confObject;
            ExcelBook = excelBook;
            WorkbookStyles = workbookStyles;
            SheetName= sheetName;
            SheetDescription= sheetDescription;
            IndexSheet = ExcelBook.CreateSheet(sheetName);
            SheetRecords = CreateListOfSheets(dBsheets);
            
        }



        private List<SheetRecord> CreateListOfSheets(List<TemplateSheetInstance> dbSheets)
        {

            var list = new List<SheetRecord>();
            using var connectionEiopa = new SqlConnection(ConfObject?.EiopaDatabaseConnectionString);

            foreach (var dbSsheet in dbSheets)
            {
                var sheetName = dbSsheet.SheetTabName.Trim();

                var sqlTab = @"select tab.TableLabel,tab.TableCode from mTable tab where tab.TableID = @tableId";
                var tab = connectionEiopa.QuerySingleOrDefault<MTable>(sqlTab, new { dbSsheet.TableID });

                var tableCodeList = tab.TableCode.Split(".").Take(4);
                var templateCode = string.Join(".", tableCodeList);
                var sqlTemplate = @"select  TemplateOrTableLabel from mTemplateOrTable tt where tt.TemplateOrTableCode = @templateCode ";

                var templateLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlTemplate, new { templateCode });
                var desc = $"{templateLabel} ## {tab.TableLabel}";

                list.Add(new SheetRecord(sheetName, desc));
            }
            return list;
        }


        public ISheet PopulateIndexSheet()
        {
            //if (ExcelBook is null || WorkbookStyles is null)
            //{
            //    return null;
            //}
            
            var titleRow = IndexSheet.CreateRow(0);
            var title = titleRow.CreateCell(0);
            title.SetCellValue(SheetDescription);
            title.CellStyle = WorkbookStyles?.TileStyle;

            var index = 2;
            foreach (var sheetRecord in SheetRecords)
            {
                var row = IndexSheet.CreateRow(index++);
                var cell = row.CreateCell(0);
                cell.SetCellValue(sheetRecord.TabSheetName);

                var link = new XSSFHyperlink(HyperlinkType.Document)
                {
                    Address = @$"'{sheetRecord.TabSheetName}'!A1"
                };
                cell.Hyperlink = link;
                cell.CellStyle = WorkbookStyles.HyperStyle;

                var titleCell = row.CreateCell(1);
                titleCell.SetCellValue(sheetRecord.Description);
                IndexSheet.SetColumnWidth(0, 5000);               

            }
            return IndexSheet;

        }
        public void AddSheet(SheetRecord sheetRecord)
        {
            SheetRecords.Add(sheetRecord);
        }

        public void RemoveSheet(string tabSheetName)
        {
            SheetRecords = SheetRecords.Where(r => r.TabSheetName != tabSheetName).ToList();
        }
        public void RemoveSheets( List<string> tabSheetNames)
        {
            foreach (var tabSheetName in tabSheetNames)
            {
                SheetRecords = SheetRecords.Where(r => r.TabSheetName != tabSheetName).ToList();
            }
            
        }

        public void Sort()
        {
            SheetRecords.Sort((SheetRecord a, SheetRecord b) => string.Compare(a.TabSheetName, b.TabSheetName));            
            SheetRecords.ForEach(sr => ExcelBook.SetSheetOrder(sr.TabSheetName.Trim(), SheetRecords.IndexOf(sr)));
        }
    }
}
