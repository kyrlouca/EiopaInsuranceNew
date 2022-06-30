
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExcelCreatorV
{
    public class ExcelHelperFunctions
    {
        static public void CopyRange(CellRangeAddress range, ISheet sourceSheet, ISheet destinationSheet)
        {
            for (var rowNum = range.FirstRow; rowNum <= range.LastRow; rowNum++)
            {
                var sourceRow = sourceSheet.GetRow(rowNum);

                if (destinationSheet.GetRow(rowNum) == null)
                    destinationSheet.CreateRow(rowNum);

                if (sourceRow != null)
                {
                    var destinationRow = destinationSheet.GetRow(rowNum);

                    for (var col = range.FirstColumn; col < sourceRow.LastCellNum && col <= range.LastColumn; col++)
                    {
                        destinationRow.CreateCell(col);
                        CopyCell(sourceRow.GetCell(col), destinationRow.GetCell(col));
                    }
                }
            }
        }

        static public void CopyColumn(string column, ISheet sourceSheet, ISheet destinationSheet)
        {
            var columnNum = CellReference.ConvertColStringToIndex(column);
            var range = new CellRangeAddress(0, sourceSheet.LastRowNum, columnNum, columnNum);
            CopyRange(range, sourceSheet, destinationSheet);
        }


        static public void CopyRows(ISheet sourceSheet, ISheet destSheet, int firstRow, int lastRow, bool IsFormattingCopied = false,int offsetRow=0)
        {
            for (var i = firstRow; i <= lastRow; i++)
            {
                var orgRow = sourceSheet.GetRow(i);
                if (orgRow is null)
                {
                    continue;
                }

                var destIdx = i + offsetRow;
                var destRow = destSheet.GetRow(destIdx);
                if (destRow is null)
                {
                    destRow = destSheet.CreateRow(destIdx);
                }

                if (!orgRow.Any())
                {
                    return;
                }

                CopyRow(orgRow, destRow, 0, IsFormattingCopied);

            }
        }

        static public IRow? CopyRow(IRow orgRow, IRow destRow, int offset = 0, bool doCopyFormatting = false)
        {

            if (orgRow is null || destRow is null)
            {
                return null;
            }
            for (var j = orgRow.FirstCellNum; j <= orgRow.LastCellNum; j++)
            {
                var destCell = destRow.GetCell(j + offset);
                if (destCell is null)
                {
                    destCell = destRow.CreateCell(j + offset);
                }

                if (doCopyFormatting)
                {
                    CopyCellWithFormating((XSSFWorkbook)destCell.Sheet.Workbook, orgRow.GetCell(j), destCell);
                }
                else
                {
                    CopyCell(orgRow.GetCell(j), destCell);
                }
            }
            return destRow;
        }



        static public IRow? CopyRowSameBook(IRow orgRow, IRow destRow, int offset = 0, bool doCopyFormatting = false)
        {

            if (orgRow is null || destRow is null)
            {
                return null;
            }
            for (var j = orgRow.FirstCellNum; j <= orgRow.LastCellNum; j++)
            {
                var destCell = destRow.GetCell(j + offset);
                if (destCell is null)
                {
                    destCell = destRow.CreateCell(j + offset);
                }

                CopyCellWithFormatingSameBook((XSSFWorkbook)destCell.Sheet.Workbook, orgRow.GetCell(j), destCell);

            }
            return destRow;
        }



        static ICell? CopyCellWithFormatingSameBook(XSSFWorkbook destBook, ICell originCell, ICell destCell)
        {
            if (destBook is null)
            {
                return null;
            }
            var cell = CopyCell(originCell, destCell);

            if (cell?.CellStyle is not null)
            {
                var originStyle = originCell.CellStyle;
                destCell.CellStyle = originStyle;
            }

            return cell;
        }


        static ICell? CopyCellWithFormating(XSSFWorkbook destBook, ICell originCell, ICell destCell)
        {
            if (destBook is null)
            {
                return null;
            }
            var cell = CopyCell(originCell, destCell);

            if (cell?.CellStyle is not null)
            {
                var originStyle = originCell.CellStyle;
                var destStyle = destBook.CreateCellStyle();
                destStyle.CloneStyleFrom(originStyle);
                destCell.CellStyle = destStyle;
            }

            return cell;
        }

        static ICell? CopyCell(ICell originCell, ICell destCell)
        {
            if (originCell is null || destCell is null)
            {
                return null;
            }

            switch (originCell.CellType)
            {
                case CellType.String:
                    {
                        var strVal = originCell?.StringCellValue ?? "";
                        destCell.SetCellValue(strVal);
                        break;
                    }
                case CellType.Numeric:
                    {
                        var numValue = originCell?.NumericCellValue ?? 0;
                        destCell.SetCellValue(numValue);
                        break;
                    }
                case CellType.Boolean:
                    {
                        var boolVal = originCell?.BooleanCellValue ?? false;
                        destCell.SetCellValue(boolVal);
                        break;
                    }
                case CellType.Formula:
                    {
                        var genVal = originCell?.CellFormula ?? "";
                        destCell.SetCellValue(genVal);
                        break;
                    }
                case CellType.Blank:
                    destCell.SetCellValue(originCell.StringCellValue);
                    break;
                case CellType.Error:
                    destCell.SetCellErrorValue(originCell.ErrorCellValue);
                    break;
                default:
                    destCell.SetCellValue(originCell.StringCellValue);
                    break;
            }
            return destCell;
        }



        static public IWorkbook CreateExcelWorkbook(string path)
        {
            IWorkbook workbook;
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                workbook = WorkbookFactory.Create(fileStream);
            }
            return workbook;
        }

        static public void SaveWorkbook(IWorkbook workbook, string path)
        {
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            workbook.Write(fileStream);
        }


        static public void MergeRegions(ISheet originSheet, ISheet destSheet, int lastRowToMerge)
        {
            // If there are are any merged regions in the source row, copy to new row
            foreach (var orgMerged in originSheet.MergedRegions)
            {
                // do NOT copy any merges after data range. There are many tables in the sheet.
                if (orgMerged.FirstRow >= lastRowToMerge)
                {
                    continue;
                }
                var destMerged = new CellRangeAddress(orgMerged.FirstRow, orgMerged.LastRow, orgMerged.FirstColumn, orgMerged.LastColumn);

                try
                {
                    destSheet.AddMergedRegion(destMerged);
                }
                catch
                {
                    //nothing really
                }

            }
        }

    }
}
