
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExcelCreatorV
{
    public static class ExcelHelperFunctions
    {

        static public void ClearRows(ISheet sheet, int startRow, int count)
        {
            //clears startRow including
            //clear by removing and inserting
            if (sheet is null || startRow < 0 || count <= 0)
            {
                return;
            }

            for (var j = startRow; j < startRow + count; j++)
            {
                var row = sheet.GetRow(j);
                if (row == null)
                {
                    continue;
                }
                sheet.RemoveRow(row);
                sheet.ShiftRows(j, j, 1);
            }
        }
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
                        CopyCellValue(sourceRow.GetCell(col), destinationRow.GetCell(col));
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



        static public IRow? CopyRow(IRow orgRow, IRow destRow, int colOffset = 0, bool doCopyFormatting = false)
        {
            if (orgRow is null || destRow is null)
            {
                return null;
            }

            for (var j = orgRow.FirstCellNum; j <= orgRow.LastCellNum; j++)
            {
                var destCell = destRow.GetCell(j + colOffset);

                if (destCell is null)
                {
                    destCell = destRow.CreateCell(j + colOffset);
                }

                if (doCopyFormatting)
                {
                    CopyCellValueWithFormating((XSSFWorkbook)destCell.Sheet.Workbook, orgRow.GetCell(j), destCell);
                }
                else
                {
                    CopyCellValue(orgRow.GetCell(j), destCell);
                }
            }
            return destRow;
        }



        static ICell? CopyCellValueWithFormating(XSSFWorkbook destBook, ICell originCell, ICell destCell)
        {
            if (destBook is null || originCell is null || destCell is null)
            {
                return null;
            }
            var cell = CopyCellValue(originCell, destCell);

            if (cell?.CellStyle is not null)
            {
                var originStyle = originCell.CellStyle;
                var destStyle = destBook.CreateCellStyle();

                destStyle.CloneStyleFrom(originStyle);
                destCell.CellStyle = destStyle;
            }

            return cell;
        }

        static ICell? CopyCellValue(ICell originCell, ICell destCell)
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

        static public void CopyManyRowsSameBook(ISheet sourceSheet, ISheet destSheet, int firstRow, int lastRow, bool IsFormattingCopied = false, int offsetRow = 0, int offsetCol = 0)
        {
            //it is useful because we do not create addtional styles in the dest book
            //copy a range of rows from orgin sheet to destination sheet 
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
                    continue;
                }

                CopyOneRowSameBook(orgRow, destRow, offsetCol, IsFormattingCopied);

            }
        }

        static public IRow? CopyOneRowSameBook(IRow orgRow, IRow destRow, int colOffset = 0, bool doCopyFormatting = false)
        {

            if (orgRow is null || destRow is null)
            {
                return null;
            }
            for (var j = orgRow.FirstCellNum; j <= orgRow.LastCellNum; j++)
            {
                var destCell = destRow.GetCell(j + colOffset);
                if (destCell is null)
                {
                    destCell = destRow.CreateCell(j + colOffset);
                }
                CopyCellSameBook(orgRow.GetCell(j), destCell, doCopyFormatting);
            }
            return destRow;
        }
        static ICell? CopyCellSameBook(ICell originCell, ICell destCell, bool doCopyFormatting)
        {
            //var workBook = (XSSFWorkbook)destCell.Sheet.Workbook;            
            if (originCell is null || destCell is null)
            {
                return null;
            }

            CopyCellValue(originCell, destCell);

            if (doCopyFormatting && originCell.CellStyle is not null)
            {
                destCell.CellStyle = originCell.CellStyle;
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


        static public void MergeRegions(ISheet originSheet, ISheet destSheet, int orgLastRowToMerge, int destRowOffset, int destColOffset)
        {
            // If there are are any merged regions in the source row, copy to new row
            foreach (var orgMerged in originSheet.MergedRegions)
            {

                // do NOT copy any merges after data range. There are many tables in the sheet.
                if (orgMerged.FirstRow > orgLastRowToMerge)
                {
                    break;
                }

                var orgRow = originSheet.GetRow(orgMerged.FirstRow);
                var destRow= destSheet.GetRow( orgMerged.FirstRow+destRowOffset);
                var orgRowView = ExcelHelperFunctions.ShowRowContents(orgRow);
                var destRowView = ExcelHelperFunctions.ShowRowContents(destRow);
                var destMerged = new CellRangeAddress(orgMerged.FirstRow + destRowOffset, orgMerged.LastRow + destRowOffset, orgMerged.FirstColumn + destColOffset, orgMerged.LastColumn + destColOffset);

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



        static public void SetColumnsWidth(ISheet destSheet, bool isOpen, int startColIdx, int endColIdx, int startDataRowIdx, int endDataRowIdx)
        {
            //Do not use default column width. destSheet.DefaultColumnWidth = 5500; somehow this crashes the program

            if (!isOpen && destSheet.GetColumnWidth(0) < ExcelSheetConstants.DefaultColumnSizeOpen)
            {
                destSheet.SetColumnWidth(0, ExcelSheetConstants.DefaultColumnSizeClosed);
            }


            //for closed tables, make the labels column large
            var colIdx = startColIdx - 2;
            if (!isOpen && colIdx >= 0)
            {
                var len = FindMaxWidth(destSheet, colIdx, startDataRowIdx, endDataRowIdx) * 256 + 900;
                var DescriptionColumnLength = Math.Min(len, ExcelSheetConstants.MaxLabelSize);
                DescriptionColumnLength = Math.Max(DescriptionColumnLength, ExcelSheetConstants.DefaultColumnSizeClosed);
                destSheet.SetColumnWidth(colIdx, DescriptionColumnLength);//set the first column to larger width
            }

            //Special case,
            if (destSheet.SheetName == "S.12.02.01.02" || destSheet.SheetName == "S.17.02.01.02")
            {
                destSheet.SetColumnWidth(colIdx, 15000);
            }


            for (var i = startColIdx; i <= endColIdx; i++)
            {
                destSheet.SetColumnWidth(i, ExcelSheetConstants.DefaultColumnSizeOpen); //5300
            }
            //for sheets that have only one data column, make the column big
            if (startColIdx == endColIdx)
            {
                var firstDataColLen = FindMaxWidth(destSheet, startColIdx, startDataRowIdx, endDataRowIdx) * 256 + 900;
                firstDataColLen = Math.Max(firstDataColLen, ExcelSheetConstants.DefaultColumnSizeClosed);
                destSheet.SetColumnWidth(startColIdx, firstDataColLen);
                Console.WriteLine("aa");
            }

        }


        static int FindMaxWidth(ISheet destSheet, int column, int startRowIdx, int endRowIdx)
        {
            var maxLen = 0;
            for (var i = startRowIdx; i <= endRowIdx; i++)
            {
                var text = destSheet?.GetRow(i)?.GetCell(column)?.ToString();
                if (text is null)
                {
                    continue;
                }

                text = text.Trim();
                maxLen = text.Length > maxLen
                    ? text.Length
                    : maxLen;

            }
            return maxLen;
        }


        public static int GetMaxNumberOfColumns(ISheet destSheet, int startRowIdx, int endRowIdx)
        {
            var maxCols = 0;
            for (var i = startRowIdx; i <= endRowIdx; i++)
            {
                var row = destSheet?.GetRow(i);
                if (row is null)
                {
                    continue;
                }
                //var lastNonEmpty = row.Cells.LastOrDefault(cell => !string.IsNullOrEmpty(cell.ToString()));
                var numberOfCols = row.Cells.Count;                    
                maxCols = Math.Max(maxCols, numberOfCols);
            }
            return maxCols;
        }

        public static string ShowRowContents(IRow row)
        {
            if(row is null)
            {
                return "null row";
            }
            var debugCells = row.Cells.Select(cell => cell?.ToString()??"").ToArray() ?? Array.Empty<string>();
            var debugViewLine = string.Join("#", debugCells);
            return debugViewLine;
        }

        public static void CreateHyperLink(ISheet sheet,WorkbookStyles workbookStyles)
        {
            var link = new XSSFHyperlink(HyperlinkType.Document)
            {
                Address = @$"'List'!A1"
            };
            var leftCell = sheet.GetRow(0).GetCell(0);

            leftCell.Hyperlink = link;
            leftCell.CellStyle = workbookStyles.HyperStyle;
        }

    }
}
