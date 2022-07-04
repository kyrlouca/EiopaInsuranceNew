using ConfigurationNs;
using Dapper;
using EntityClasses;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

using TransactionLoggerNs;

namespace ExcelCreatorV
{
    internal enum LineState { Start, SheetCodeState, ZetState, ColumnState, RowState, ErrorState, EndState };
    internal enum LineType { Empty, AnyText, SheetCode, Zet, Column, Row }//Code is template code "PF.01.01.02"
    public class ExcelFileCreator
    {
        //public string DebugTableCode { get; set; } = "S.23.01.01.01";
        public string DebugTableCode { get; set; } = "";

        public ConfigObject ConfigObject { get; private set; }


        public bool IsFileValid { get; internal set; } = true;
        public bool IsValidEiopaVersion { get; internal set; } = true;
        public string? ExcelTemplateFile { get; internal set; }
        public string ExcelOutputFile { get; internal set; }

        public XSSFWorkbook? ExcelTemplateBook { get; private set; }
        public XSSFWorkbook DestExcelBook { get; private set; } = new XSSFWorkbook();
        public WorkbookStyles WorkbookStyles { get; private set; }


        public int DocumentIdInput { get; }
        public DocInstance? Document { get; internal set; }
        public int DocumentId => Document?.InstanceId ?? 0;
        public string? ModuleCode { get; internal set; }
        public int PensionFundId { get; internal set; }
        public bool IsPensionFundLarge { get; internal set; } = false;
        public int UserId { get; }
        public int ApplicableYear { get; internal set; }
        public int ApplicableQuarter { get; internal set; }
        public bool IsYearlyDocumentx { get; set; }
        public int ModuleId { get; internal set; }
        public int Status { get; internal set; }
        public int TablesScanned { get; internal set; } = 0;
        public string[]? FilesScanned { get; internal set; }
        public string SolvencyVersion { get; internal set; }



        public static void CreateTheExcelFile(string solvencyVersion, int userId, int documentId, string excelOutputFile)
        {
            var excelCreator = new ExcelFileCreator(solvencyVersion, userId, documentId, excelOutputFile);
            excelCreator.CreateExcelFile();
        }

        private ExcelFileCreator(string solvencyVersion, int userId, int documentId, string excelOutputFile)
        {
            Console.WriteLine($"***&&& Creator");
            SolvencyVersion = solvencyVersion;
            DocumentIdInput = documentId;
            UserId = userId;
            IsValidEiopaVersion = Configuration.IsValidVersion(SolvencyVersion);
            ExcelOutputFile = excelOutputFile;
            ConfigObject = GetConfiguration();


            WorkbookStyles = new WorkbookStyles(DestExcelBook);
        }


        private bool CreateExcelFile()
        {
            Console.WriteLine($"in Create Excel file");
            if (!IsValidEiopaVersion)
            {
                var errorMessage = $"Invalid Solvency Version :{SolvencyVersion} for Document Id:{DocumentIdInput}";
                Log.Error(errorMessage);
                Console.WriteLine(errorMessage);
                Console.WriteLine("valide versions: IU250, IU260");
                return false;
            }

            //if (GetConfiguration() is null)
            //{
            //    return false;
            //}

            Document = HelperInsuranceFunctions.InsuranceData.GetDocumentById(DocumentIdInput);
            if (Document is null)
            {
                Log.Error($"Invalid Document Id:{DocumentIdInput}");
                return false;
            }
            var isLockedDocument = Document.Status.Trim() == "P";
            if (isLockedDocument)
            {
                var messg = $"DocumentId: {DocumentId}. Document currently being Processed by another User";
                Log.Error(messg);
                var trans = new TransactionLog()
                {
                    PensionFundId = Document.PensionFundId,
                    ModuleCode = Document.ModuleCode,
                    ApplicableYear = Document.ApplicableYear,
                    ApplicableQuarter = Document.ApplicableQuarter,
                    Message = messg,
                    UserId = 0,
                    ProgramCode = ProgramCode.CX.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = Document.InstanceId,
                    MessageType = MessageType.ERROR.ToString()
                };
                TransactionLogger.LogTransaction(SolvencyVersion, trans);

                return false;
            }




            ModuleId = Document.ModuleId;
            ModuleCode = Document.ModuleCode;
            PensionFundId = Document.PensionFundId;
            ApplicableYear = Document.ApplicableYear;
            ApplicableQuarter = Document.ApplicableQuarter;

            WriteProcessStarted();

            if (!OpenExcelTemplate())
            {
                Console.WriteLine($"Cannot OPEN the Template. Aborting...");
                return false;
            };

            //** Read the ExcelFile with the templates and create a new ExcelBook where each sheet in Db is an excel tabsheet
            //** the new ExcelBook is updated with facts from the Db            
            CreateDestinationExcelBook(DebugTableCode);


            //Save Excel File
            if (SaveDestExcelFile())
            {
                return false;
            }

            Console.WriteLine("finished");
            return true;
        }


        private void CreateDestinationExcelBook(string debugTableCode = "")
        {
            //select all the sheets from the db and create a tab for each sheet
            //For multiZet tables, several sheets may resutl from a  single tablecode "S.21.01.01.01" (one for each zet)
            if (ExcelTemplateBook is null)
                return;

            using var connectionLocalDb = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var sqlSheets = @"
                SELECT
                  TemplateSheetInstance.TemplateSheetId
                 ,TemplateSheetInstance.InstanceId
                 ,TemplateSheetInstance.SheetCode
                 ,TemplateSheetInstance.SheetTabName
                 ,TemplateSheetInstance.TableCode
                 ,TemplateSheetInstance.TableID
                 ,TemplateSheetInstance.IsOpenTable
                 ,TemplateSheetInstance.YDimValFromExcel
                 ,TemplateSheetInstance.ZDimValFromExcel
                 ,TemplateSheetInstance.YDimVal
                 ,TemplateSheetInstance.ZDimVal
                FROM dbo.TemplateSheetInstance
                WHERE TemplateSheetInstance.InstanceId = @documentID
                ORDER BY TemplateSheetInstance.SheetTabName
                ";
            var sheets = connectionLocalDb.Query<TemplateSheetInstance>(sqlSheets, new { DocumentId }).ToList();
            if (!string.IsNullOrEmpty(debugTableCode))
            {
                sheets = sheets.Where(sheet => sheet.TableCode.Trim() == debugTableCode).ToList();
                Console.WriteLine($"**** Debugging-- Create ONLY the sheet: {debugTableCode} ");
            }

            var singleExcelSheets = new List<SingleExcelSheet>();
            foreach (var sheet in sheets)
            {

                var sqlCount = @"select COUNT(*) as cnt from TemplateSheetFact fact where fact.TemplateSheetId = @TemplateSheetId";
                var countFacts = connectionLocalDb.QuerySingleOrDefault<int>(sqlCount, new { sheet.TemplateSheetId });
                if (countFacts == 0)
                {
                    var mesage = $"Empty Sheet without facts. sheet: {sheet.TemplateSheetId}-{sheet.SheetCode}";
                    Console.WriteLine(mesage);
                    Log.Information(mesage);
                    continue;//Empty sheets should NOT be reported. If there were empty sheets in the xbrl (and the db) Do NOT create excel sheets for them anyway
                }
                //********************************************************
                var newSheet = new SingleExcelSheet(SolvencyVersion, ExcelTemplateBook, WorkbookStyles, DestExcelBook, sheet);
                var rowsInserted = newSheet.FillSingleExcelSheet();
                singleExcelSheets.Add(newSheet);
                Console.WriteLine($"\n{sheet.SheetCode} rows:{rowsInserted}");
            }

            var sheetList = CreateListOfSheets(sheets);

            //---------------------------------------------------------------
            //Create a sheet which combines S0.06.02.01.01 with S0.06.02.01.02
            var sheetS06Name = "S.06.02.01 Combined";
            var sheetS06Combined = new SheetS0601Combined(ConfigObject, DestExcelBook, sheetS06Name, WorkbookStyles);
            sheetS06Combined.CreateS06CombinedSheet();
            if (!sheetS06Combined.IsEmpty)
            {
                sheetList.Add(("S.06.02.01 Combined", "List of assets ## Information on positions held ##  Information on assets"));
                sheetList.Sort();
                var sheetS602idx = sheetList.Select(item => item.sheetName).ToList().IndexOf(sheetS06Name);
                DestExcelBook.SetSheetOrder(sheetS06Name, sheetS602idx);
            }

            //---------------------------------------------------------------

            //********            
            //Get the specialSheets from the db table
            string[] headerSheets = { "S.20.01.01.01" };

            foreach (var headerSheet in headerSheets)
            {
                var childSheets = singleExcelSheets.Where(sheet => sheet.SheetDb.TableCode == headerSheet).ToList();
                if (1 == 2)
                {
                    var mergedSheet = MergeSheets(childSheets, headerSheet);
                    sheetList.Add((headerSheet, "List of assets ## Information on positions held ##  Information on assets"));
                }

            }

            //*****************************************


            var listSheet = CreateListSheet(sheetList);

            DestExcelBook.SetSheetOrder(listSheet.SheetName, 0);
            DestExcelBook.SetActiveSheet(0);
            listSheet.SetZoom(80);

        }


        private ISheet MergeSheets(List<SingleExcelSheet> sheetsToMerge, string destSheetName)
        {
            var mergeSheet = DestExcelBook.CreateSheet(destSheetName);

            foreach (var childSheet in sheetsToMerge)
            {
                AppendSingleSheet(childSheet, mergeSheet);
            }
            return mergeSheet;
        }

        private void AppendSingleSheet(SingleExcelSheet singleExcelSheet, ISheet mergedSheet)
        {
            var orgSheet = singleExcelSheet.DestSheet;
            var mergedOffset = 10;
            var mergedStartPosition = mergedSheet.LastRowNum == 0 ? 0 : mergedSheet.LastRowNum + mergedOffset;
            ExcelHelperFunctions.CopyRowsSameBook(orgSheet, mergedSheet, orgSheet.FirstRowNum, orgSheet.LastRowNum, true, mergedStartPosition);
            ExcelHelperFunctions.MergeRegions(orgSheet, mergedSheet, orgSheet.LastRowNum, mergedStartPosition, 0);
            ExcelHelperFunctions.SetColumnsWidth(mergedSheet, singleExcelSheet.SheetDb.IsOpenTable, singleExcelSheet.StartColDestIdx, singleExcelSheet.EndColDestIdx, singleExcelSheet.DestDataRange.FirstRow, singleExcelSheet.DestDataRange.LastRow);
        }


        private List<(string sheetName, string desctiption)> CreateListOfSheets(List<TemplateSheetInstance> sheets)
        {

            var list = new List<(string sheetName, string sheetLabel)>();
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            foreach (var sheet in sheets)
            {
                var sheetName = sheet.SheetTabName.Trim();

                var sqlTab = @"select tab.TableLabel,tab.TableCode from mTable tab where tab.TableID = @tableId";
                var tab = connectionEiopa.QuerySingleOrDefault<MTable>(sqlTab, new { sheet.TableID });

                var tableCodeList = tab.TableCode.Split(".").Take(4);
                var templateCode = string.Join(".", tableCodeList);
                var sqlTemplate = @"select  TemplateOrTableLabel from mTemplateOrTable tt where tt.TemplateOrTableCode = @templateCode ";

                var templateLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlTemplate, new { templateCode });
                var desc = $"{templateLabel} ## {tab.TableLabel}";

                list.Add((sheetName, desc));
            }
            return list;
        }


        private ISheet CreateListSheet(List<(string sheetName, string sheetLable)> sheets)
        {

            var listSheet = DestExcelBook.CreateSheet("List");
            var titleRow = listSheet.CreateRow(0);
            var title = titleRow.CreateCell(0);
            title.SetCellValue("List of Templates");
            title.CellStyle = WorkbookStyles.TileStyle;

            var index = 2;
            foreach (var (sheetName, sheetLable) in sheets)
            {
                var row = listSheet.CreateRow(index++);
                var cell = row.CreateCell(0);
                cell.SetCellValue(sheetName);

                var link = new XSSFHyperlink(HyperlinkType.Document)
                {
                    Address = @$"'{sheetName}'!A1"
                };
                cell.Hyperlink = link;
                cell.CellStyle = WorkbookStyles.HyperStyle;

                var titleCell = row.CreateCell(1);
                titleCell.SetCellValue(sheetLable);
            }
            listSheet.SetColumnWidth(0, 5000);

            return listSheet;

        }

        private ConfigObject GetConfiguration()
        {

            var ConfigObject = Configuration.GetInstance(SolvencyVersion).Data;
            if (string.IsNullOrEmpty(ConfigObject.LoggerExcelWriterFile))
            {
                var errorMessage = "LoggerExcelWriter is not defined in ConfigData.json";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(ConfigObject.LoggerExcelWriterFile, rollOnFileSizeLimit: true, shared: true, rollingInterval: RollingInterval.Day)
                .CreateLogger();


            if (!Configuration.IsValidVersion(SolvencyVersion))
            {
                var errorMessage = $"Excel Writer --Invalid Eiopa Version: {SolvencyVersion}";
                Console.WriteLine(errorMessage);
                Log.Error(errorMessage);
                throw new SystemException(errorMessage);
            }

            //the connection strings depend on the Solvency Version
            if (string.IsNullOrEmpty(ConfigObject.EiopaDatabaseConnectionString) || string.IsNullOrEmpty(ConfigObject.LocalDatabaseConnectionString))
            {
                var errorMessage = "Empty ConnectionStrings in ConfigData.json file";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }

            return ConfigObject;
        }

        private void WriteProcessStarted()
        {
            var message = $"Excel Writer Started -- Document:{DocumentId} Fund:{PensionFundId} ModuleId:{ModuleCode} Year:{ApplicableYear} Quarter:{ApplicableQuarter} Solvency:{SolvencyVersion}";
            Console.WriteLine(message);
            Log.Information(message);

            TransactionLog trans;
            trans = new TransactionLog()
            {
                PensionFundId = PensionFundId,
                ModuleCode = ModuleCode,
                ApplicableYear = ApplicableYear,
                ApplicableQuarter = ApplicableQuarter,
                Message = message,
                UserId = UserId,
                ProgramCode = ProgramCode.CX.ToString(),
                ProgramAction = ProgramAction.INS.ToString(),
                InstanceId = DocumentId,
                MessageType = MessageType.INFO.ToString()
            };
            TransactionLogger.LogTransaction(SolvencyVersion, trans);
        }

        private bool OpenExcelTemplate()
        {
            // ** open the excel as filestream
            ExcelTemplateFile = ConfigObject.ExcelTemplateFileGeneral;

            Console.WriteLine($"using template -- : {ExcelTemplateFile}");

            if (!File.Exists(ExcelTemplateFile))
            {
                var messagef = $"File does NOT exist: {ExcelTemplateFile}";

                Log.Error(messagef);
                IsFileValid = false;
                var xtrans = new TransactionLog()
                {
                    PensionFundId = PensionFundId,
                    ModuleCode = ModuleCode,
                    ApplicableYear = ApplicableYear,
                    ApplicableQuarter = ApplicableQuarter,
                    Message = messagef,
                    UserId = UserId,
                    ProgramCode = ProgramCode.CX.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = DocumentId,
                    MessageType = MessageType.ERROR.ToString(),
                    FileName = ExcelTemplateFile
                };
                TransactionLogger.LogTransaction(SolvencyVersion, xtrans);
                return false;
            }
            //open Excel from Filestream
            try
            {
                //ICreationHelper helper = DestExcelTemplateBook.GetCreationHelper();
                Console.WriteLine($"opening$$$");
                using var FS = new FileStream(ExcelTemplateFile, FileMode.Open, FileAccess.Read);
                ExcelTemplateBook = (XSSFWorkbook)WorkbookFactory.Create(FS);
            }
            catch (Exception ex)
            {
                var messagef = $"---------- Can NOT Open Excel Template File :{ExcelTemplateFile} --{ex.Message} ";
                Console.WriteLine(messagef);
                Log.Error(messagef);
                return false;
            }
            return true;
        }

        private bool SaveDestExcelFile()
        {
            try
            {

                var fs = File.Create(ExcelOutputFile);
                //var outStream = new FileStream(outFile, FileMode.Create, FileAccess.Write);

                var xmlProps = DestExcelBook.GetProperties();
                var coreProps = xmlProps.CoreProperties;
                coreProps.Creator = "Novum International ICSS XBRL";

                DestExcelBook.Write(fs);

            }
            catch (Exception e)
            {
                var messages = $"File Reader Cannot Create file {ExcelOutputFile}";
                Log.Error($"{messages} --EXCEPTION {e.Message}");
                return false;
            }
            return true;
        }



    }
}