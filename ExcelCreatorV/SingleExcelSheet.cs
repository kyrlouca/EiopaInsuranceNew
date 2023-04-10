
using Dapper;
using EntityClasses;
using GeneralUtilsNs;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HelperInsuranceFunctions;
using Shared.Services;

namespace ExcelCreatorV
{
    public class SingleExcelSheet
    {

        public const int DestDataRowPositionShort = 13;
        public const int DestDataRowPositionLong = 16;

        public IConfigObject ConfigObjectR { get; private set; }
        public ConfigData ConfigDataR { get => ConfigObjectR.Data; }

        public TemplateSheetInstance SheetDb { get; private set; }
        public XSSFWorkbook ExcelTemplateBook { get; private set; }
        public XSSFWorkbook DestExcelBook { get; private set; }
        

        public ISheet? DestSheet { get; set; }
        public ISheet OriginSheet { get; set; }
        public CellRangeAddress OrgDataRange { get; set; }
        public CellRangeAddress? OrgExtendedRange;

        public int OffsetRow { get; set; }
        public int OffsetRowInsToDelete { get; set; }

        public int OffsetCol { get; set; }
        public CellRangeAddress DestDataRange { get; set; }
        public CellRangeAddress? DestExtendedRange;

        public int StartColDestIdx { get; set; }
        public int EndColDestIdx { get; set; }

        public WorkbookStyles WorkbookStyles { get; }

        //static int DefaultColumnSizeClosed { get; } = 5500;
        //static int DefaultColumnSizeOpen { get; } = 5300;
        //static int MaxLabelSize { get; } = 17000;
        public bool isTesting = false;

        public SingleExcelSheet(IConfigObject configObject, XSSFWorkbook excelTemplateBook, WorkbookStyles workbookStyles, XSSFWorkbook destExcelBook, TemplateSheetInstance sheetDb)
        {
            ConfigObjectR = configObject;
            ExcelTemplateBook = excelTemplateBook;
            //Styles = styles;
            WorkbookStyles = workbookStyles;
            DestExcelBook = destExcelBook;

            SheetDb = sheetDb;
            
            var originSheetName = SheetDb.TableCode.Split(".").ToList().GetRange(0, 4); //excel sheet tabs have only tηe  first 4 parts of the sheet code
            var filingSheetCode = string.Join(".", originSheetName).Trim();

            OriginSheet = ExcelTemplateBook.GetSheet(filingSheetCode);            

        }

        public int FillSingleExcelSheet()
        {
            //Table mTemplateOrTable has the field TD which defines the range of data. For Open tables only the first Row            
            //First, copy the extended range which starts from the left corner where the "SheetCode" is  up to the last cell of the dataRange
            //The copied range in the destination sheet  will start from ZERO point at the dest sheet
            //Then, fill the values of the *datarange* which will start from zero in the dest sheet             

            var lines = 0;

            //*************************************
            //sheetCodePosition is shifted to the top left corner ( row= and col=0 col) in the destination excel sheet.
            //All the rows copied from the origin to the dest will be shifted 

            (OffsetRow, OffsetCol) = FindSheetCodePosition(SheetDb, OriginSheet);            

            var templateOrTable = GetTableOrTemplate(SheetDb);


            OrgDataRange = CellRangeAddress.ValueOf(templateOrTable.TD); //TD has the range for data                        
            OrgExtendedRange = new CellRangeAddress(OffsetRow, OrgDataRange.LastRow, OffsetCol, OrgDataRange.LastColumn); //starts from Sheetcode up to last data cell            

            DestExtendedRange = new CellRangeAddress(0, OrgExtendedRange.LastRow - OffsetRow, 0, OrgExtendedRange.LastColumn - OffsetCol);
            DestDataRange = new CellRangeAddress(OrgDataRange.FirstRow - OffsetRow, OrgDataRange.LastRow - OffsetRow, OrgDataRange.FirstColumn - OffsetCol, OrgDataRange.LastColumn - OffsetCol);


            var tabName = SheetDb.SheetTabName.Trim();

            DestSheet = DestExcelBook.CreateSheet(tabName);

            //**********************************
            //* copy extended range from the sheetCode  up to the last row of the original template
            CopyExtentedRange();

            SetGlobalDestStartColumnIdx();

            FormatColumnRow(OrgDataRange.FirstRow - OffsetRow - 1); //column row is one row above data range

            //**********************************
            //*CLOSED tables : Fill up the cells with values 
            if (!SheetDb.IsOpenTable)
            {
                //some facts in addition to row, col they have a pivot zet value (many facts in the same cell- kind of 3rd dimension)
                var factPivotZets = GetFactPivotZets();
                if (SheetDb.TableCode == "S.12.02.01.02" || SheetDb.TableCode == "S.17.02.01.02")
                {
                    lines = UpdateClosed_ZetAsRows_12and17(factPivotZets);
                }
                else
                {
                    lines = UpdateClosedTableValues(factPivotZets);
                }
            };

            //**********************************
            //*OPEN Tables :Fill the cells with values
            if (SheetDb.IsOpenTable)
            {
                lines = UpdateOpenTableValues();
            }

            ExcelHelperFunctions.SetColumnsWidth(DestSheet, SheetDb.IsOpenTable, StartColDestIdx, EndColDestIdx, DestDataRange.FirstRow, DestDataRange.LastRow);

            //**********************************
            // do NOT copy any merges after data range.
            var lastRowToMerge = OrgDataRange.FirstRow;
            

            //**********************************
            //inserting titles must be last because it insert rows and messes up the offsetrow
            var addedLines = WriteSheetTopTitlesAndZetNew();


            //**********************************
            //apply merge after shifting rows. it is a bug            
            var finalRowOffset = OffsetRow - addedLines;
            ExcelHelperFunctions.MergeRegions(OriginSheet, DestSheet, lastRowToMerge, -finalRowOffset, -OffsetCol);

            DestSheet.SetZoom(80);

            ExcelHelperFunctions.CreateHyperLink(DestSheet, WorkbookStyles);
            var yx = DestExcelBook.NumCellStyles;

            return lines;

            static string CopyCellTypedValue(ICell originCell, ICell destCell)
            {
                if (originCell is null || destCell is null)
                {
                    return "";
                }
                var errorMessage = "";
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
                            var numValue = originCell?.NumericCellValue ?? 999;
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
                            destCell.SetCellValue("");
                            break;
                        }
                    case CellType.Blank:
                        break;
                    default:
                        destCell.SetCellValue("Error");
                        errorMessage = $"Error copying Cell value.";
                        break;
                }

                return errorMessage;
            }

            void CopyUpperRow(IRow originRow, int originRowNum, int destRowNum,bool copyStyle =false)
            {
                var destRow = DestSheet.CreateRow(destRowNum);
                var debugdest=ExcelHelperFunctions.ShowRowContents(originRow);                

                for (var x = OrgExtendedRange.FirstColumn; x <= OrgDataRange.LastColumn; x++) //no need to go beyond 
                {
                    var originCell = originRow?.GetCell(x);
                    if (originCell is not null)
                    {
                        var destColNum = x - OffsetCol;
                        var destCell = destRow.CreateCell(destColNum);

                        if (copyStyle)
                        {
                            var originStyle = originCell.CellStyle;
                            var destStyle = DestExcelBook.CreateCellStyle();
                            destStyle.CloneStyleFrom(originStyle);
                            destCell.CellStyle = destStyle;
                        }
                        CopyCellTypedValue(originCell, destCell);
                    }
                }
                return;
            }

            void CopyDataRow(IRow originRow, int originRowNum, int destRowNum)
            {
                var destRow = DestSheet.CreateRow(destRowNum);
                var debugdest = ExcelHelperFunctions.ShowRowContents(destRow);                

                for (var x = OrgExtendedRange.FirstColumn; x <= OrgExtendedRange.LastColumn; x++)
                {
                    //extended range is the whole area which starts from sheetCode up to last row
                    //data range is is the range with data as defined in mTable

                    var originCell = originRow?.GetCell(x);
                    if (x < OrgDataRange.FirstColumn) //get the row and row labels column
                    {
                        var destColNumL = x - OffsetCol;
                        var destCellL = destRow.CreateCell(destColNumL);

                        if (originCell is not null)
                        {
                            CopyCellTypedValue(originCell, destCellL);
                        }
                        else
                        {
                            destCellL.SetCellValue("");
                        }

                        if (x == OrgDataRange.FirstColumn - 1)//rows
                        {
                            destCellL.CellStyle = WorkbookStyles.FullBorderStyle;
                        }
                        if (x == OrgDataRange.FirstColumn - 2 && originCell is not null)//row labels
                        {
                            var originStyle = originCell.CellStyle;
                            var destStyle = DestExcelBook.CreateCellStyle();
                            destStyle.CloneStyleFrom(originStyle);
                            destStyle.WrapText = false;
                            destCellL.CellStyle = destStyle;
                        }

                    }

                    else //data range => no need to copy style, need take care of cells with diagonal  border
                    {
                        var destColNumD = x - OffsetCol;
                        var destCellD = destRow.CreateCell(destColNumD);


                        if (originCell?.CellStyle.BorderDiagonal == BorderDiagonal.Both)
                        {
                            //destCellD.SetCellValue("diaal");
                            destCellD.CellStyle = WorkbookStyles.ShadedStyle;
                            //destCellD.SetCellValue("@");
                            destCellD.SetBlank();
                        }
                        else
                        {
                            destCellD.CellStyle = WorkbookStyles.BasicBorderStyle;
                        }



                    }
                }
            }

            void CopyExtentedRange()
            {
                //var dRow = 0;
                var lastRow = SheetDb.IsOpenTable ? OrgExtendedRange.LastRow - 1 : OrgExtendedRange.LastRow; //we do not want to copy the empty line for open tables
                for (var y = OrgExtendedRange.FirstRow; y <= lastRow; y++)
                {
                    var originRow = OriginSheet.GetRow(y);

                    var debugCells = originRow?.Cells?.Select(cell => cell?.ToString()).ToArray() ?? Array.Empty<string>();
                    var debugViewLine = string.Join(",", debugCells);

                    if (originRow is null)
                    {
                        continue;
                    }

                    //copy one row
                    var destRowNum = y - OffsetRow;
                    if (y < OrgDataRange.FirstRow) // the area above data, copy style for bold, etc for NOT NULL                
                    {
                        var xx = ExcelHelperFunctions.ShowRowContents(originRow);
                        var isCopyStyle = destRowNum > 4;
                        CopyUpperRow(originRow, y, destRowNum,isCopyStyle);
                    }
                    else
                    {
                        CopyDataRow(originRow, y, destRowNum);
                    }
                    var yd = DestExcelBook.NumCellStyles;
                }
            }

            void FormatColumnRow(int rowOfColLabelsDestIdx)
            {
                var rowOfColLabels = DestSheet.GetRow(rowOfColLabelsDestIdx);
                var rowAboveCols = DestSheet.GetRow(rowOfColLabelsDestIdx - 1);
                for (var i = StartColDestIdx; i <= EndColDestIdx; i++)
                {
                    var colLabelCell = rowOfColLabels.GetCell(i);
                    colLabelCell.CellStyle = WorkbookStyles.ColumnLabelStyle;
                    if (i == StartColDestIdx)//the first 
                    {
                        CellUtil.SetCellStyleProperty(colLabelCell, CellUtil.BORDER_LEFT, BorderStyle.Thick);
                    }

                    if (SheetDb.IsOpenTable)
                    {
                        var cellAbove = rowAboveCols.GetCell(i);
                        cellAbove.CellStyle = WorkbookStyles.ColumnOpenLabelStyle;
                        if (i == StartColDestIdx)//the first 
                        {
                            CellUtil.SetCellStyleProperty(cellAbove, CellUtil.BORDER_LEFT, BorderStyle.Thick);
                        }
                    }
                }

            }

        }

        int WriteSheetTopTitlesAndZetNew()
        {
            if(DestSheet is null)
            {
                return 0;
            }

            //at the top of each sheet             
            //-- write the sheetcode,  titles
            //-- write all the Z values of the sheet  
            // the shift function shifts also the starting row
            var connectionEiopa = new SqlConnection(ConfigDataR.EiopaDatabaseConnectionString);
            var connectionInsurance = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);

            //******************************************************
            //the first row of the datarange should be always fixed
            //therefore, insert a few rows (ShiftRows) below the sheetcode 
            //Actually we have two different fixed positions. the long one is applied when sheets has more than 3 dims
            var sqlZet = @"select sheet.Dim,sheet.Value from SheetZetValue sheet where sheet.TemplateSheetId = @sheetId";
            var sheetZetDims = connectionInsurance.Query<SheetZetValue>(sqlZet, new { sheetId = SheetDb.TemplateSheetId }).ToList();

            var zetDimsCount = Math.Min(sheetZetDims.Count, 6);
            var destFixedDataRow = zetDimsCount > 3 ? DestDataRowPositionLong : DestDataRowPositionShort;
            var rowsToShift = destFixedDataRow - DestDataRange.FirstRow;
            if (rowsToShift > 0)
            {
                DestSheet.ShiftRows(DestSheet.FirstRowNum + 1, DestSheet.LastRowNum, rowsToShift);
            }
            //************************************************


            var sqlTab = @"select tab.TableLabel, tab.TableCode from mTable tab where tab.TableID = @tableId";
            var table = connectionEiopa.QuerySingleOrDefault<MTable>(sqlTab, new { tableId = SheetDb.TableID });

            var tableCodeList = table.TableCode.Split(".").Take(4);
            var templateCode = string.Join(".", tableCodeList);
            var sqlTemplate = @"select  TemplateOrTableLabel from mTemplateOrTable tt where tt.TemplateOrTableCode = @templateCode ";
            var templateLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlTemplate, new { templateCode });

            var sheetCodeCell = WriteCellInRow(0, 0, SheetDb.TableCode);
            sheetCodeCell.CellStyle = WorkbookStyles.TitleH2Style;

            var titleCell = WriteCellInRow(1, 0, templateLabel);
            titleCell.CellStyle = WorkbookStyles.TitleH2Style;

            var subTitleCell = WriteCellInRow(2, 0, table.TableLabel);
            subTitleCell.CellStyle = WorkbookStyles.TitleH2Style;

            var emptyRowIdx = 3;
            WriteCellInRow(3, 0, "");


            for (var i = 0; i < sheetZetDims.Count; i++)
            {
                var zetDim = sheetZetDims[i];
                var sqlMember = @"
                        select 
                         mem.MemberXBRLCode , mem.MemberLabel 
                         from mMember mem 
                        join mDomain dom on dom.DomainID=mem.DomainID
                        where mem.MemberXBRLCode = @memCode";

                var member = connectionEiopa.QueryFirstOrDefault<MMember>(sqlMember, new { memCode = zetDim.Value }) ?? new MMember();
                WriteCellInRow(emptyRowIdx + 1 + i, 0, $"{sheetZetDims[i].Dim.Trim()} -- {sheetZetDims[i].Value.Trim()}");
                updateCell(emptyRowIdx + 1 + i, 1, $"{member.MemberLabel}");
            }

            //clear the rows above the first title row (up to the z rows)
            //for some tables we need to leave some additinal lines above the titles. For example for  "S.25.01.01.01" leave 7 lines intact
            var specialTables = new Dictionary<string, int>()
            {
                { "S.25.01.01.01",7},
                { "S.25.01.01.03",7},
                { "S.25.01.01.04",7},
                { "S.29.03.01.01",7},
                { "S.29.03.01.03",7},
            };

            var linesToSpare = 5; //includes the column row and the title rows above the datarange
            if (specialTables.ContainsKey(table.TableCode))
            {
                linesToSpare = specialTables[table.TableCode];
            }
            var rowsToDelete = destFixedDataRow - (emptyRowIdx + zetDimsCount) - linesToSpare;
            ExcelHelperFunctions.ClearRows(DestSheet, emptyRowIdx + 1 + zetDimsCount, rowsToDelete);

            clearSubtitle(subTitleCell, emptyRowIdx, destFixedDataRow - 3);

            return rowsToShift;

            ICell WriteCellInRow(int row, int col, string val)
            {
                var rowLine = DestSheet.GetRow(row);

                if (rowLine is not null)
                {
                    DestSheet.RemoveRow(rowLine);
                }
                rowLine = DestSheet.CreateRow(row);
                var cell = rowLine.CreateCell(col);
                cell.SetCellValue($"{val}");
                return cell;
            }

            ICell updateCell(int row, int col, string val)
            {
                var rowLine = DestSheet.GetRow(row) ?? DestSheet.CreateRow(row);
                var cell = rowLine.GetCell(col) ?? rowLine.CreateCell(col);
                cell.SetCellValue($"{val}");
                return cell;
            }

            void clearSubtitle(ICell subTitleCell, int startingIdx, int endIdx)
            {
                var subtitleTrim = subTitleCell?.ToString()?.Trim();
                if (subtitleTrim is null) return;

                for (var i = startingIdx; i <= endIdx; i++)
                {
                    var cellval = DestSheet?.GetRow(i)?.GetCell(0);
                    if (cellval is not null && cellval?.ToString()?.Trim() == subtitleTrim)
                    {
                        DestSheet?.GetRow(i)?.RemoveCell(cellval);
                    }
                }
            }
        }

        private void WriteMultiZetFactLabels(List<string> factZetList)
        {
            //Normally facts have a row and col
            //But some tables such as S.02.02.01.02 have facts which have an additional zet (for currency for example)
            //Since excel is two dimensional, create an additional column for each zet value found in the facts
            //factlist : elements 2c_dim:OC(s2c_CU:GBP)                
            if (factZetList.Count == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(factZetList[0]))
            {
                return; // facts can have empty zet which result in a list with one empty element
            }

            //two rows above because 1 row above are the col labels
            var zetRow = DestSheet?.GetRow(DestDataRange.FirstRow - 2)
                ?? DestSheet.CreateRow(DestDataRange.FirstRow - 2);

            //write the zet values as labels one row above the column labels
            for (var i = 0; i < factZetList.Count; i++)
            {
                var colIdx = DestDataRange.FirstColumn + i;
                var zetLabelCell = zetRow.GetCell(colIdx) ?? zetRow.CreateCell(colIdx);
                var zetLabel = GetDomainLabel(factZetList[i]);

                zetLabelCell.SetCellValue(zetLabel); //GBP

                zetLabelCell.CellStyle = WorkbookStyles.ColumnLabelStyle;
                if (i == 0)
                {
                    CellUtil.SetCellStyleProperty(zetLabelCell, CellUtil.BORDER_LEFT, BorderStyle.Thick);
                }
                if (i > 0)
                {
                    DestSheet.SetColumnWidth(colIdx, ExcelSheetConstants.DefaultColumnSizeClosed);
                }

            }
        }

        private void SetGlobalDestStartColumnIdx()
        {
            var rowOfColLabelsDestIdx = OrgDataRange.FirstRow - OffsetRow - 1;

            //startColIdx and endColIdx refer to the dest sheet
            //The start for the data range
            //for closed tables is easy, for open
            StartColDestIdx = !SheetDb.IsOpenTable ?
                 OrgDataRange.FirstColumn - OffsetCol :
                 FindFirstColumnOpenTable(DestSheet.GetRow(rowOfColLabelsDestIdx), OrgDataRange.LastColumn);
            StartColDestIdx = StartColDestIdx < 0 ? 1000 : StartColDestIdx; //if not found for open tables make it big            
            EndColDestIdx = DestSheet.GetRow(rowOfColLabelsDestIdx).LastCellNum - 1; //the function returns PLUS one, so minus 1
        }

        private List<string> GetFactPivotZets()
        {
            using var connectionInsurance = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);
            var sqlZetValues = @"select distinct fact.Zet from TemplateSheetFact fact where fact.TemplateSheetId = @sheetId order by fact.Zet";
            var pivotZets = connectionInsurance.Query<string>(sqlZetValues, new { sheetId = SheetDb.TemplateSheetId }).ToList() ?? new List<string>();
            return pivotZets;
        }

        private int UpdateClosedTableValues(List<string> factZetList)
        {
            //some tables have cells which contain more than one fact (the fact has row, col, zet)  -multi zet facts
            //for those tables, which normally contain just ONE columen, create additinal columns for each z value
            var lines = 0;
            var rowofColumnLabelsIdx = OrgDataRange.FirstRow - 1;
            var rowOfColumnLabels = OriginSheet.GetRow(rowofColumnLabelsIdx);

            WriteMultiZetFactLabels(factZetList);//if facts have more than on zet then write the z on top of col labels


            //go through each ROW
            for (var y = OrgDataRange.FirstRow; y <= OrgDataRange.LastRow; y++)
            {
                lines++;
                var originRow = OriginSheet.GetRow(y);
                var orgRowLabelCell = originRow.GetCell(OrgDataRange.FirstColumn - 1);//Get the row label (ROO10) one cell left of the data 
                                                                                      //var rowLabel = originRow.GetCell(OrgDataRange.FirstColumn - 1)?.StringCellValue; //Get the row label (ROO10) one cell left of the data 
                var rowLabel = orgRowLabelCell?.StringCellValue;

                var destRow = DestSheet?.GetRow(y - OffsetRow);
                if (destRow is null)
                {
                    continue;
                }

                var destRowLabelCell = destRow.GetCell(OrgDataRange.FirstColumn - OffsetCol - 1);
                destRowLabelCell.CellStyle = WorkbookStyles.RowLabelStyle;
                if (y == OrgDataRange.FirstRow)
                {
                    CellUtil.SetCellStyleProperty(destRowLabelCell, CellUtil.BORDER_TOP, BorderStyle.Thin);
                }

                //go through each COLUMN
                for (var x = OrgDataRange.FirstColumn; x <= OrgDataRange.LastColumn; x++)
                {
                    //each cell may have more than one facts because of zet. created additional cell next to the cell
                    var colLabel = rowOfColumnLabels.GetCell(x).ToString();// we need the original column label for all additional cells                    
                    for (var zIdx = 0; zIdx < factZetList.Count; zIdx++)
                    {
                        var destCellColIdx = x - OffsetCol + zIdx;
                        var destCellNew = destRow.GetCell(destCellColIdx);
                        destCellNew ??= destRow.CreateCell(destCellColIdx);

                        if (string.IsNullOrEmpty(rowLabel))
                        {
                            //for rows which have ONLY titles and NO fact cells, continue
                            continue;
                        }
                   
                        if(destCellNew.ToString() == "@") {
                            destCellNew.CellStyle = WorkbookStyles.ShadedStyle;
                            //destCellNew.SetCellValue("");
                            destCellNew.SetBlank();

                        }


                        //if (destCellNew.StringCellValue == "@")
                        //{
                        //    destCellNew.CellStyle = WorkbookStyles.ShadedStyle;
                        //    destCellNew.SetCellValue("");
                        //}


                        var zetValue = factZetList[zIdx];
                        var fact = FindFactFromRowColZet(SheetDb, rowLabel, colLabel, zetValue);
                        if (fact is not null)
                        {
                            UpdateExcelCellWithValue(fact, destCellNew);
                        }

                    }
                }
            }
            return lines;
        }

        private int UpdateClosed_ZetAsRows_12and17(List<string> factZetList)
        {
            //this is a customized method to handle multizet with multi columns
            //The template for 12.02.01.02 was actually changed 
            //basically we create one ROW for each zet value

            var rowOfColumnLabelsIdx = OrgDataRange.FirstRow - 1;
            var rowOfColumnLabels = OriginSheet.GetRow(rowOfColumnLabelsIdx);

            var FirstOrgRowIdx = OrgDataRange.FirstRow; //always the same row (for all zet values)
            var FirstOrgRow = OriginSheet.GetRow(FirstOrgRowIdx);

            var rowLabel = FirstOrgRow.GetCell(OrgDataRange.FirstColumn - 1)?.StringCellValue; //Get the row label (ROO10) one cell left of the data             


            //just to format the left top cell with borders
            var destLabelRow = DestSheet.GetRow(rowOfColumnLabelsIdx - OffsetRow);
            var leftCell = destLabelRow.GetCell(OrgDataRange.FirstColumn - OffsetCol - 1);
            leftCell.CellStyle = WorkbookStyles.ColumnLabelStyle;
            CellUtil.SetCellStyleProperty(leftCell, CellUtil.BORDER_LEFT, BorderStyle.Thick);



            //create one ROW for each Zet            
            var lines = 0;
            for (var zz = 0; zz < factZetList.Count; zz++)
            {
                lines++;

                //copy the same row for each zet value
                var destRowIdx = FirstOrgRowIdx + zz - OffsetRow;
                var destRow = DestSheet.GetRow(destRowIdx);
                destRow ??= DestSheet.CreateRow(destRowIdx);

                //write the row Label (R0040) to the left of the datarange
                var rowLabelIdx = OrgDataRange.FirstColumn - OffsetCol - 1;//one cell to the left is the row label
                var rowLabelCell = destRow.GetCell(rowLabelIdx);
                rowLabelCell ??= destRow.CreateCell(rowLabelIdx);
                rowLabelCell.SetCellValue(rowLabel);
                rowLabelCell.CellStyle = WorkbookStyles.ColumnLabelStyle;
                if (zz == 0)
                {
                    CellUtil.SetCellStyleProperty(rowLabelCell, CellUtil.BORDER_LEFT, BorderStyle.Thick);
                }


                //write the cell value to the left of the datarange
                var zetCellIdx = OrgDataRange.FirstColumn - OffsetCol - 2;//one cell to the left is the row label                               
                var zetCell = destRow.GetCell(zetCellIdx);
                zetCell ??= destRow.CreateCell(zetCellIdx);

                var zetLabel = GetDomainLabel(factZetList[zz]);
                zetCell.SetCellValue(zetLabel);
                zetCell.CellStyle = WorkbookStyles.BasicBorderStyle;

                //go through each COLUMN
                for (var cIdx = OrgDataRange.FirstColumn; cIdx <= OrgDataRange.LastColumn; cIdx++)
                {
                    var colLabel = rowOfColumnLabels.GetCell(cIdx).ToString();// we need the original column label for all additional cells                    

                    var destCellColIdx = cIdx - OffsetCol;
                    var destCellNew = destRow.GetCell(destCellColIdx);
                    destCellNew ??= destRow.CreateCell(destCellColIdx);

                    var zetValue = factZetList[zz];
                    var fact = FindFactFromRowColZet(SheetDb, rowLabel, colLabel, zetValue);
                    destCellNew.CellStyle = WorkbookStyles.BasicBorderStyle;
                    if (fact is not null)
                    {
                        UpdateExcelCellWithValue(fact, destCellNew);
                    }

                }
            }

            return lines;
        }

        int UpdateOpenTableValues()
        {
            var lines = 0;
            //*********** OPEN Tables :Fill the cells with values

            //for open tables the  TD on mTemplateOrTable does not specify the rows range of course
            //it only specifies the first row which help us to find the row wich has the column labels
            //instead, read all the distinct rows from the FACTs if the table is open

            var rowWithColumnLabelsIdx = OrgDataRange.FirstRow - 1;
            var rowWithcolumnLabels = OriginSheet.GetRow(OrgDataRange.FirstRow - 1);
            var firstColumnIndex = FindFirstColumnOpenTable(rowWithcolumnLabels, OrgDataRange.LastColumn);

            using var connectionInsurance = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);
            var sqlRows = @"select  distinct fact.Row from TemplateSheetFact fact  where  fact.TemplateSheetId= @sheetId order by fact.Row";
            var rowLabels = connectionInsurance.Query<string>(sqlRows, new { sheetId = SheetDb.TemplateSheetId }).ToList();


            var rowIndex = 0;
            foreach (var rowLabel in rowLabels)
            {
                lines++;
                var destRowIndex = rowWithColumnLabelsIdx + 1 - OffsetRow + rowIndex;
                var destRow = DestSheet?.GetRow(destRowIndex);
                destRow ??= DestSheet?.CreateRow(destRowIndex);
                Console.Write("!");

                for (var x = firstColumnIndex; x <= OrgDataRange.LastColumn; x++)
                {
                    var colLabel = OriginSheet.GetRow(rowWithColumnLabelsIdx).GetCell(x).StringCellValue;    //from origin therefore no shifting                  
                    var colIndex = x + OffsetCol;

                    var lbl = $"{rowIndex}={rowLabel},{colIndex}={colLabel}";
                    var destCell = destRow?.GetCell(colIndex);
                    if (destCell is null)
                    {
                        destCell = destRow?.CreateCell(colIndex);
                        destCell?.SetCellValue("");
                        destCell.CellStyle = WorkbookStyles.BasicBorderStyle;
                    }

                    var facts = FindFactsFromRowCol(SheetDb, rowLabel, colLabel);
                    if (facts.Count > 0)
                    {
                        var fact = facts.First(); //should'nt get more than one for open (no multicurrency facts)
                        UpdateExcelCellWithValue(fact, destCell);
                    }

                }
                rowIndex++;
            }


            return lines;
        }

        private (int row, int col) FindSheetCodePosition(TemplateSheetInstance sheet, ISheet xSheet)
        {
            //We need to find the position where the sheetCode is written
            //It will be us  ed as an offset to copy the data range to the top left corner of the new sheet
            //findIndex will not return the true column number since null cells do NOT appear in the structure
            var templateOrTable = GetTableOrTemplate(sheet);
            var eiopaSheetData = CellRangeAddress.ValueOf(templateOrTable.TD); //TD has the range for data


            for (var i = xSheet.FirstRowNum; i < xSheet.LastRowNum; i++)
            {
                var rowNew = xSheet.GetRow(i);
                if (rowNew is null)
                {
                    continue;
                }

                var x = xSheet.GetRow(i).Cells.Select(cell => cell?.ToString());
                var debugViewLine = string.Join(",", x);

                var rowSelect = xSheet.GetRow(i);
                if (rowSelect.Cells.FindIndex(cell => cell.ToString() == sheet.TableCode.Trim()) > -1)
                {

                    for (var j = rowSelect.FirstCellNum; j < rowSelect.LastCellNum; j++)
                    {
                        var pp = rowSelect.GetCell(j);
                        if (pp is not null)
                        {
                            if (pp?.ToString()?.Trim() == sheet.TableCode.Trim())
                            {
                                var vv = pp.StringCellValue;
                                var frow = i;
                                var fcol = j;
                                return (frow, fcol);
                            }

                        }
                    }
                }


            }
            Console.WriteLine("SheetCode not found");
            return (0, 0);

        }

        private ΜTemplateOrTable GetTableOrTemplate(TemplateSheetInstance sheet)
        {
            var sqlTemplate = @"select top 1 TC,TD from mTemplateOrTable temp where temp.TemplateOrTableCode = @tableCode";
            using var connectionEiopa = new SqlConnection(ConfigDataR.EiopaDatabaseConnectionString);
            var template = connectionEiopa.QuerySingleOrDefault<ΜTemplateOrTable>(sqlTemplate, new { tableCode = sheet.TableCode });
            return template;

        }

        private static int FindFirstColumnOpenTable(IRow columnsRow, int lastColumn)
        {

            var firstColIndex = -1;
            for (var j = lastColumn; j >= 0; j--)
            {
                var colValue = columnsRow.GetCell(j)?.StringCellValue ?? "";
                if (!GeneralUtils.IsMatch(EiopaConstants.RegexConstants.ColRowRegEx, colValue))
                {
                    firstColIndex = j;
                    break;
                }
            }
            return firstColIndex + 1;

        }

        private List<TemplateSheetFact> FindFactsFromRowCol(TemplateSheetInstance sheet, string row, string col)
        {
            //more than one fact with the same row,col but with different currency
            var sqlFact =
          @"
            SELECT
                  fact.FactId
                 ,fact.Row
                 ,fact.Col
                 ,fact.Zet
                 ,fact.CurrencyDim
                 ,fact.IsRowKey
                 ,fact.IsShaded
                 ,fact.NumericValue
                 ,fact.DateTimeValue
                 ,fact.BooleanValue
                 ,fact.Decimals
                 ,fact.TextValue
                 ,fact.DataType
                 ,fact.DataTypeUse
                FROM dbo.TemplateSheetFact fact
                WHERE fact.TemplateSheetId = @sheetId
                AND fact.Row = @row
                AND fact.Col = @col                
                    ";

            using var connectionLocalDb = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);
            var facts = connectionLocalDb.Query<TemplateSheetFact>(sqlFact, new { sheetId = sheet.TemplateSheetId, row, col })?.ToList() ?? new List<TemplateSheetFact>();
            return facts;
        }

        private TemplateSheetFact FindFactFromRowColZet(TemplateSheetInstance sheet, string row, string col, string zet)
        {
            //more than one fact with the same row,col but with different currency
            var sqlFact =
          @"
            SELECT
                  fact.FactId
                 ,fact.Row
                 ,fact.Col
                 ,fact.Zet
                 ,fact.CurrencyDim
                 ,fact.IsRowKey
                 ,fact.IsShaded
                 ,fact.NumericValue
                 ,fact.DateTimeValue
                 ,fact.BooleanValue
                 ,fact.Decimals
                 ,fact.TextValue
                 ,fact.DataType
                 ,fact.DataTypeUse
                FROM dbo.TemplateSheetFact fact
                WHERE fact.TemplateSheetId = @sheetId
                AND fact.Row = @row
                AND fact.Col = @col                
                AND fact.Zet = @zet                
                    ";

            using var connectionLocalDb = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);
            var fact = connectionLocalDb.QueryFirstOrDefault<TemplateSheetFact>(sqlFact, new { sheetId = sheet.TemplateSheetId, row, col, zet });
            return fact;
        }

        private void UpdateExcelCellWithValue(TemplateSheetFact fact, ICell cell)
        {
            using var connectionEiopaDb = new SqlConnection(ConfigDataR.EiopaDatabaseConnectionString);
            if (cell is null) return;
            if (fact is null) return;

            //var DataTypeUse = (fact is null) ? "NULL" : fact.DataTypeUse.Trim();
            var DataTypeUse = fact.DataTypeUse.Trim();

            //N-,E-,S-,D- ,P-,M-,B- ,I-
            var isDiagonal = cell.CellStyle == WorkbookStyles.ShadedStyle;
            if (isDiagonal)
            {
                //var x = 33;
            }
            switch (DataTypeUse)
            {
                case "D": //date
                    cell.SetCellValue(fact.DateTimeValue);
                    cell.CellStyle = WorkbookStyles.DateStyle;
                    break;
                case "B": //boolean
                    cell.SetCellValue(fact.BooleanValue);
                    cell.CellStyle = WorkbookStyles.TextStyle;
                    break;
                case "N": //Numeric (Decimal) 
                case "M": //monetary
                    cell.SetCellValue((double)fact.NumericValue);
                    cell.CellStyle = WorkbookStyles.RealStyle;
                    break;
                case "P": //Percent
                    cell.SetCellValue((double)fact.NumericValue);
                    cell.CellStyle = WorkbookStyles.PercentStyle;
                    break;
                case "S": //String
                    cell.SetCellValue(fact.TextValue);
                    cell.CellStyle = WorkbookStyles.TextStyle;
                    break;
                case "E": // Enumeration/Code"
                          //mMember
                    var sqlMember = "select mem.MemberLabel from mMember mem where mem.MemberXBRLCode = @xbrlCode";
                    var memDescription = connectionEiopaDb.QuerySingleOrDefault<string>(sqlMember, new { xbrlCode = fact.TextValue });
                    cell.SetCellValue(memDescription);
                    cell.CellStyle = WorkbookStyles.TextStyle;

                    break;
                case "I": //integer
                    cell.SetCellValue((int)Math.Floor(fact.NumericValue));
                    cell.CellStyle = WorkbookStyles.IntStyle;
                    break;
                case "NULL"://fact is null                            
                    break;
                default:
                    cell.SetCellValue("ERROR:" + fact.TextValue);
                    break;
            }




        }

        private string GetDomainLabel(string domainString)
        {
            using var connectionEiopa = new SqlConnection(ConfigDataR.EiopaDatabaseConnectionString);
            var xbrlCode = DimDom.GetParts(domainString).DomAndValRaw;
            var sqlMem = @"select mem.MemberLabel from mMember mem where MemberXBRLCode = @xbrlCode";
            var val = connectionEiopa.QuerySingleOrDefault<string>(sqlMem, new { xbrlCode });
            return val;
        }


    }
}
