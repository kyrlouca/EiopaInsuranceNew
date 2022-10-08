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
    public record struct IndexSheetListItem
    {
        //public ISheet Sheet { get; init; }
        public string TabSheetName { get; init; }
        public string Description { get; init; }
        public IndexSheetListItem(string tabSheetName, string description)
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
        List<IndexSheetListItem> SheetRecords { get; set; } = new List<IndexSheetListItem>();
        //public IndexSheetList(ConfigObject confObject, XSSFWorkbook excelBook, WorkbookStyles workbookStyles, List<TemplateSheetInstance> dBsheets, string sheetName, string sheetDescription)
        public IndexSheetList(ConfigObject confObject, XSSFWorkbook excelBook, WorkbookStyles workbookStyles, string sheetName, string sheetDescription)
        {
            ConfObject = confObject;
            ExcelBook = excelBook;
            WorkbookStyles = workbookStyles;
            SheetName = sheetName;
            SheetDescription = sheetDescription;
            IndexSheet = ExcelBook.CreateSheet(sheetName);
            //SheetRecords = CreateListOfSheets(dBsheets);

        }



        public List<IndexSheetListItem> CreateSheetRecordsFromDb(List<TemplateSheetInstance> dbSheets)
        {

            var list = new List<IndexSheetListItem>();
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

                list.Add(new IndexSheetListItem(sheetName, desc));
            }
            SheetRecords = list;
            return list;
        }


        public ISheet PopulateIndexSheet()
        {

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
                cell.CellStyle = WorkbookStyles?.HyperStyle;

                var titleCell = row.CreateCell(1);
                titleCell.SetCellValue(sheetRecord.Description);
                IndexSheet.SetColumnWidth(0, 7000);

            }
            return IndexSheet;

        }
        public void AddSheetRecord(IndexSheetListItem sheetRecord)
        {
            SheetRecords.Add(sheetRecord);
        }

        public void RemoveSheet(string tabSheetName)
        {
            SheetRecords = SheetRecords.Where(r => r.TabSheetName != tabSheetName).ToList();
        }
        public void RemoveSheets(List<string> tabSheetNames)
        {
            foreach (var tabSheetName in tabSheetNames)
            {
                var shIdx = ExcelBook.GetSheetIndex(tabSheetName);
                if (shIdx == -1)
                {
                    continue;
                }
                ExcelBook.RemoveAt(shIdx);
                var shrIdx = SheetRecords.FirstOrDefault(r => r.TabSheetName == tabSheetName);

                if (shIdx > -1)
                {
                    SheetRecords.Remove(shrIdx);
                }
                else
                {
                    Console.WriteLine($"sheet {tabSheetName} not found");
                }

            }
        }

        public void SortSheetRecords()
        {
            SheetRecords.Sort((IndexSheetListItem a, IndexSheetListItem b) => string.Compare(a.TabSheetName, b.TabSheetName));
            SheetRecords.ForEach(sr => ExcelBook.SetSheetOrder(sr.TabSheetName.Trim(), SheetRecords.IndexOf(sr)));
        }
    }
}
