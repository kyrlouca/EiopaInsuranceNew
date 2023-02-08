using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using Shared.Services;

namespace ExcelCreatorV
{

    public class SheetS0601Combined
    {

        public XSSFWorkbook WorkingExcelWorkbook { get; private set; }
        public ConfigData ConfigDataR { get; private set; }
        public string SheetS63Name { get; }
        public WorkbookStyles WorkbookStyles;

        public ISheet SheetS61 { get; }
        public ISheet SheetS62 { get; }
        public ISheet SheetS63 { get; internal set; }
        public bool IsEmpty { get; internal set; } = false;


        public SheetS0601Combined(ConfigData configObject, XSSFWorkbook workingExcelWorkbook, string sheetName, WorkbookStyles workbookStyles)
        {
            ConfigDataR = configObject;
            SheetS63Name = sheetName;
            WorkingExcelWorkbook = workingExcelWorkbook;
            WorkbookStyles = workbookStyles;


            SheetS61 = WorkingExcelWorkbook.GetSheet("S.06.02.01.01");
            SheetS62 = WorkingExcelWorkbook.GetSheet("S.06.02.01.02");

        }

        public void CreateS06CombinedSheet()
        {


            if (SheetS61 is null || SheetS62 is null)
            {
                IsEmpty = true;
                return;
            }

            SheetS61.CopyTo(WorkingExcelWorkbook, $"{SheetS63Name}", true, true);
            SheetS63 = WorkingExcelWorkbook.GetSheet(SheetS63Name);

            var s61ColRowIdx = FindColumnRow(SheetS61, "C0001");
            var s61ColRow = SheetS61.GetRow(s61ColRowIdx);
            var offset = s61ColRow.LastCellNum + 1;


            //copy the titles
            var s62ColumnsRow = FindColumnRow(SheetS62, "C0040");
            for (var i = 0; i <= s62ColumnsRow; i++)
            {
                var s62Row = SheetS62.GetRow(i);
                var s63Row = SheetS63.GetRow(i);
                ExcelHelperFunctions.CopyRow(s62Row, s63Row, offset, true);
            }


            //Copy the lines (linked from s62 to s61
            for (var i = s61ColRowIdx + 1; i <= SheetS61.LastRowNum; i++)
            {
                var s61Row = SheetS61.GetRow(i);
                var key = s61Row.GetCell(1).StringCellValue;
                var s62RowIdx = FindS62LinkedRow(SheetS62, s61ColRowIdx + 1, key);
                if (s62RowIdx > 0)
                {
                    //Console.WriteLine(key);
                    Console.Write("*");
                    var s62Row = SheetS62.GetRow(s62RowIdx);
                    var s63Row = SheetS63.GetRow(i);
                    if (s63Row is null)
                    {
                        continue;
                    }
                    ExcelHelperFunctions.CopyOneRowSameBook(s62Row, s63Row, offset, true);
                }

            }

            var startColIdx = 0;
            var endColIdx = 50;

            for (var i = startColIdx; i <= endColIdx; i++)
            {
                SheetS63.SetColumnWidth(i, 5300); //5300
            }

            CreateHyperLink();
            SheetS63.SetZoom(80);

            //var yx = WorkingExcelWorkbook.NumCellStyles;

        }

        private static int FindColumnRow(ISheet sheet, string colLabel)
        {
            for (var i = sheet.FirstRowNum; i <= sheet.LastRowNum; i++)
            {
                var cell = sheet.GetRow(i)?.GetCell(0);
                if (cell is not null && cell?.StringCellValue == colLabel)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int FindS62LinkedRow(ISheet sheet, int startingRow, string key)
        {
            for (var i = startingRow; i <= sheet.LastRowNum; i++)
            {
                var cell = sheet.GetRow(i)?.GetCell(0);
                if (cell is not null && cell?.StringCellValue == key)
                {
                    return i;
                }
            }
            return -1;

        }

        void CreateHyperLink()
        {
            var link = new XSSFHyperlink(HyperlinkType.Document)
            {
                Address = @$"'List'!A1"
            };
            var leftCell = SheetS63.GetRow(0).GetCell(0);

            leftCell.Hyperlink = link;
            leftCell.CellStyle = WorkbookStyles.HyperStyle;
        }

    }
}
