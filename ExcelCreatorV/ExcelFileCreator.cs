using ConfigurationNs;
using Dapper;
using EiopaConstants;
using EntityClasses;
using Microsoft.Data.SqlClient;
using Dapper;
using NPOI.SS.Formula;
using NPOI.SS.UserModel;
using NPOI.Util.ArrayExtensions;
using NPOI.XSSF.UserModel;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using TransactionLoggerNs;
using Org.BouncyCastle.Bcpg;
using System.Collections;
using NPOI.Util;

namespace ExcelCreatorV
{
    internal enum LineState { Start, SheetCodeState, ZetState, ColumnState, RowState, ErrorState, EndState };
    internal enum LineType { Empty, AnyText, SheetCode, Zet, Column, Row }//Code is template code "PF.01.01.02"

    internal readonly record struct MergedSheetRecord
    {
        public ISheet? TabSheet { get; init; }
        public List<TemplateSheetInstance> ChildrenSheetInstances { get; init; }
        public string SheetDescription { get; init; }
        public MergedSheetRecord(ISheet? tabSheet, string sheetDescription, List<TemplateSheetInstance> childrenSheetInstances)
        {
            TabSheet = tabSheet;
            SheetDescription = sheetDescription;
            ChildrenSheetInstances = childrenSheetInstances;
        }
    }

    internal readonly record struct TemplateBundle
    {
        public string TemplateCode { get; init; }
        public string TemplateDescription { get; init; }
        public List<String> TableCodes { get; init; }
        public TemplateBundle(string templateTableCode, string templateDescription, List<string> tableCodes)
        {
            TemplateCode = templateTableCode;
            TemplateDescription = templateDescription;
            TableCodes = tableCodes;
        }
    }


    public class SpecialHorizontalTemplate
    {
        public string TemplateCode { get; init; }
        public string TemplateName { get; init; }
        public String[][] TableCodesArray { get; init; }
        public List<List<string>> TableCodes { get; init; }
        public SpecialHorizontalTemplate(string templateCode, string templateName, String[][] tableCodes)
        {
            TemplateCode = templateCode;
            TemplateName = templateName;
            TableCodesArray = tableCodes;
            TableCodes = TableCodesArray.Select(tc => tc.ToList()).ToList();
        }

    }



    public class ExcelFileCreator
    {
        //public string DebugTableCode { get; set; } = "S.29.04.01.01";
        public string DebugTableCode { get; set; } = "";

        public ConfigObject ConfigObject { get; private set; }


        public bool IsFileValid { get; internal set; } = true;
        public bool IsValidEiopaVersion { get; internal set; } = true;
        public string? ExcelTemplateFile { get; internal set; }
        public string ExcelOutputFile { get; internal set; }

        public XSSFWorkbook? ExcelTemplateBook { get; private set; }
        public XSSFWorkbook DestExcelBook { get; private set; } = new XSSFWorkbook();
        public WorkbookStyles WorkbookStyles { get; private set; }

        internal IndexSheetList IndexSheetList { get; init; }

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
            IndexSheetList = new IndexSheetList(ConfigObject, DestExcelBook, WorkbookStyles, "List", "List of Templates");
        }


        private bool CreateExcelFile()
        {
            Console.WriteLine($"in Create Excel file");
            if (ConfigObject is null)
            {
                var errMessage = $"Cannot create ConfigObject";
                Console.WriteLine(errMessage);
                return false;
            }

            Document = GetDocumentById(DocumentIdInput);
            Console.WriteLine("after getDocId");

            if (Document.InstanceId == 0)
            {
                var errMessage = $"Invalid Document Id:{DocumentIdInput}";
                Console.WriteLine(errMessage);
                Log.Error(errMessage);
                return false;
            }
            Console.WriteLine("after getDocId:");
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


            Console.WriteLine("after GetDocId");

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

            ///*************************************************************
            ///*** One Sheet for each table. Later we can merge
            var singleExcelSheets = new List<SingleExcelSheet>();
            ///*************************************************************
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

            IndexSheetList.CreateSheetRecordsFromDb(sheets);

            //---------------------------------------------------------------
            //Create a sheet which combines S0.06.02.01.01 with S0.06.02.01.02
            var sheetS06Name = "S.06.02.01 Combined";
            var sheetS06Combined = new SheetS0601Combined(ConfigObject, DestExcelBook, sheetS06Name, WorkbookStyles);
            sheetS06Combined.CreateS06CombinedSheet();
            if (!sheetS06Combined.IsEmpty)
            {
                IndexSheetList.AddSheetRecord(new IndexSheetListItem("S.06.02.01 Combined", "List of assets ## Information on positions held ##  Information on assets"));
            }

            //*****************************************************************
            //*****  Create Merged Sheets
            MergeTemplateSheetsUniversal();


            //*******************************************
            IndexSheetList.SortSheetRecords();
            IndexSheetList.PopulateIndexSheet();
            IndexSheetList.IndexSheet.SetZoom(80);

            DestExcelBook.SetSheetOrder("List", 0);
            DestExcelBook.SetActiveSheet(0);

        }




        private ISheet CreateMergedSheet(List<List<ISheet>> sheetsToMerge, string destSheetName)
        {

            //Each iteration of the outer List will add VERTICALLY a list of HORIZONTAL sheets

            var destSheetIdx = DestExcelBook.GetSheetIndex(destSheetName);
            var destSheet = destSheetIdx == -1 ? DestExcelBook.CreateSheet(destSheetName) : DestExcelBook.GetSheetAt(destSheetIdx);
            var rowGap = 4;

            //write horizontally a list of sheets
            var rowOffset = 0;
            foreach (var sheetList in sheetsToMerge)
            {
                AppendHorizontalSheets(sheetList, destSheet, rowOffset, 1);
                rowOffset = destSheet.LastRowNum + rowGap;
            }

            //set columns width            
            var firstRow = destSheet.GetRow(0) ?? destSheet.CreateRow(0);


            for (int i = 0; i < 25; i++)
            {
                var cell = firstRow?.GetCell(i) ?? firstRow?.CreateCell(i);
            }
            //**Fucking a
            if (1 == 2)
            {
                destSheet.SetColumnWidth(0, 12000);
                destSheet.SetColumnWidth(1, 2000);
                for (var j = 2; j < firstRow?.Cells.Count; j++)
                {
                    destSheet.SetColumnWidth(j, 5000);
                }
            }
            //*************
            return destSheet;
        }

        private static ISheet AppendHorizontalSheets(List<ISheet> sheetsToMerge, ISheet destSheet, int rowOffset, int colGap)
        {
            //add each sheet in the list  HORIZONTALLY one after the other
            //var colGap = 2;
            var totalColOffset = 0;
            foreach (var childSheet in sheetsToMerge)
            {
                if (childSheet is null)
                {
                    continue;
                }

                ExcelHelperFunctions.CopyManyRowsSameBook(childSheet, destSheet, childSheet.FirstRowNum, childSheet.LastRowNum, true, rowOffset, totalColOffset);
                var childColOffset = ExcelHelperFunctions.GetMaxNumberOfColumns(childSheet, childSheet.FirstRowNum, childSheet.LastRowNum);
                totalColOffset += childColOffset + colGap;

            }
            return destSheet;
        }

        private ConfigObject GetConfiguration()
        {

            var configObject = Configuration.GetInstance(SolvencyVersion).Data;
            if (string.IsNullOrEmpty(configObject.LoggerExcelWriterFile))
            {
                var errorMessage = "LoggerExcelWriter is not defined in ConfigData.json";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(configObject.LoggerExcelWriterFile, rollOnFileSizeLimit: true, shared: true, rollingInterval: RollingInterval.Day)
                .CreateLogger();


            if (!Configuration.IsValidVersion(SolvencyVersion))
            {
                var errorMessage = $"Excel Writer --Invalid Eiopa Version: {SolvencyVersion}";
                Console.WriteLine(errorMessage);
                Log.Error(errorMessage);
                throw new SystemException(errorMessage);
            }

            //the connection strings depend on the Solvency Version
            if (string.IsNullOrEmpty(configObject.EiopaDatabaseConnectionString) || string.IsNullOrEmpty(configObject.LocalDatabaseConnectionString))
            {
                var errorMessage = "Empty ConnectionStrings in ConfigData.json file";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }

            return configObject;
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


        public DocInstance GetDocumentById(int documentId)
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            Console.WriteLine($" a2 fuck in GetDocId : {documentId}, conf:{ConfigObject.LocalDatabaseConnectionString}");
            var emptyDocument = new DocInstance();
            //var sqlFundx = "select doc.InstanceId, doc.Status,doc.IsSubmitted, doc.ApplicableYear,doc.ApplicableQuarter, doc.ModuleCode,doc.ModuleId, doc.PensionFundId,doc.UserId from DocInstance doc where doc.InstanceId=@documentId";

            var sqlFund2 = @"
                SELECT
                  doc.InstanceId
                 ,doc.ModuleId            
                FROM dbo.DocInstance doc
                WHERE doc.InstanceId = @documentId

                ";


            var doc = connectionInsurance.QuerySingleOrDefault<DocInstance>(sqlFund2, new { documentId });
            if (doc is null)
            {
                Console.WriteLine($"documentId:{documentId} does not exist");
                return emptyDocument;
            }
            Console.WriteLine($"documentId:{documentId} is valid");
            return doc;

        }


        public void MergeTemplateSheetsUniversal()
        {
            //Merge sheets for each templeate Code (3 digit code) based on dimension .(line of business BL and currency OC)
            //If there is a TemplateBundel, the Merged sheet can merge horizontally and vertically.
            //A bundle contains the template code and a list of horizontal tableCodes lists like {S.19.01.01, {S.19.01.01.01,19.01.01.02,etc},{19.01.01.08}}
            var templates = CreateTemplateTableBundles(ConfigObject, ModuleId);
            //templates = templates.Where(bundle => (bundle.TemplateCode == "S.05.02.01" || bundle.TemplateCode == "S.19.01.01")).ToList();

            foreach (var template in templates)
            {
                MergeOneTemplate(template);
            }
        }

        private void MergeOneTemplate(TemplateBundle templateTableBundle)
        {
            //One template may have many Zet dimensions(for business line or currency)
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            //currency is can be CD,CR,OC but for s.19 is oc

            var sqlZet = @"
                    SELECT zet.value
                    FROM TemplateSheetInstance sheet
                    JOIN SheetZetValue zet ON zet.TemplateSheetId = sheet.TemplateSheetId
                    WHERE sheet.InstanceId = @documentId
                        AND sheet.TableCode LIKE @templateCode
                        AND zet.Dim IN ('BL','OC','CR')
                    GROUP BY zet.Value
            ";
            var templateCode = $"{templateTableBundle.TemplateCode}%";
            var zetBLList = connectionInsurance.Query<string>(sqlZet, new { DocumentId, templateCode }).ToList();


            if (!zetBLList.Any())
            {
                zetBLList.Add("");
            }
            foreach (var zetBlValue in zetBLList)
            {
                var mergedRecord = MergeOneZetTemplate(templateTableBundle, zetBlValue);
                if (mergedRecord.TabSheet is null)
                {
                    continue;
                }

                mergedRecord.TabSheet.SetZoom(80);
                ExcelHelperFunctions.CreateHyperLink(mergedRecord.TabSheet, WorkbookStyles);
                var sheetsToRemove = mergedRecord.ChildrenSheetInstances.Select(sheet => sheet.SheetTabName.Trim()).ToList();
                IndexSheetList.RemoveSheets(sheetsToRemove);
                IndexSheetList.AddSheetRecord(new IndexSheetListItem(mergedRecord.TabSheet.SheetName, mergedRecord.SheetDescription));

            }
        }
        private MergedSheetRecord MergeOneZetTemplate(TemplateBundle templateBundle, string zetBLValue)
        {
            List<SpecialHorizontalTemplate> specials = new()
            {
                new SpecialHorizontalTemplate("S.05.02.01", "S.05.02.01", new[] { new string[] { "S.05.02.01.01", "S.05.02.01.02", "S.05.02.01.03" }, new string[] { "S.05.02.01.04", "S.05.02.01.05", "S.05.02.01.06" } }),
                new SpecialHorizontalTemplate("S.19.01.01", "S.19.01.01", new[] {
                    new string[] { "S.19.01.01.01", "S.19.01.01.02", "S.19.01.01.03", "S.19.01.01.04", "S.19.01.01.05" ,"S.19.01.01.06" },
                    new string[] { "S.19.01.01.07", "S.19.01.01.08", "S.19.01.01.09", "S.19.01.01.10", "S.19.01.01.11" ,"S.19.01.01.12" },
                    new string[] { "S.19.01.01.13", "S.19.01.01.14", "S.19.01.01.15", "S.19.01.01.16", "S.19.01.01.17" ,"S.19.01.01.18" },
                    new string[] { "S.19.01.01.19" },
                    new string[] { "S.19.01.01.20" },
                    new string[] { "S.19.01.01.21" },
                })
            };

            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var mergedTabName = string.IsNullOrEmpty(zetBLValue)
                ? templateBundle.TemplateCode
                : templateBundle.TemplateCode + "#" + zetBLValue;
            mergedTabName = mergedTabName.Replace(":", "_");

            // each tableCode may have several dbSheets because of Zets other than business line and currency            
            List<List<TemplateSheetInstance>> dbSheets = new();
            var tableCodes = templateBundle.TableCodes;

            //A specialTemplate has horizontal tables in the same sheet.
            var specialTemplate = specials.FirstOrDefault(special => special.TemplateCode == templateBundle.TemplateCode);

            if (specialTemplate is not null)
            {
                //the specialTemplate example here two horizontal lists with 3 tables each in this case
                // "S.05.02.01.01", "S.05.02.01.02", "S.05.02.01.03" , 
                // "S.05.02.01.04", "S.05.02.01.05", "S.05.02.01.06"                 
                foreach (var horizontalDbList in specialTemplate.TableCodes)
                {
                    var horizontalDbTables = horizontalDbList.Select(tableCode => getOrCreateDbSheet(ConfigObject, DocumentId, tableCode, zetBLValue).FirstOrDefault()).ToList();
                    dbSheets.Add(horizontalDbTables);
                }
            }
            else
            {
                //We can have more than one sheet for the same Business line, Currency , if the table has dimensions
                //Need to merge also
                dbSheets = tableCodes.Select(tableCode => getOrCreateDbSheet(ConfigObject, DocumentId, tableCode, zetBLValue)).ToList();
            }

            var dbRealSheets = dbSheets.SelectMany(sheet => sheet).Where(sheet => sheet.TableID != -1);
            var countReal = dbRealSheets.Count();
            if (countReal == 0 || (countReal == 1 && string.IsNullOrEmpty(zetBLValue)))
            {
                //Do not create merge if
                //All the sheets in this template where created artificially.
                //Only One Sheet but without zet as businessLine
                //if there is just one sheet or
                return new MergedSheetRecord(null, mergedTabName, new List<TemplateSheetInstance>());
            }

            //iSheets is a list of lists. Each inner list has the sheets which lay horizontally
            var iSheets = dbSheets.Select(tableCodeSheets => tableCodeSheets.Select(dbSheet => GetSheetFromBook(dbSheet)).ToList()).ToList();

            var mergedSheet = CreateMergedSheet(iSheets, mergedTabName);

            ExcelHelperFunctions.CreateHyperLink(mergedSheet, WorkbookStyles);

            var dbSheetFlatList = dbSheets.SelectMany(sheet => sheet).AsList();

            return new MergedSheetRecord(mergedSheet, mergedTabName, dbSheetFlatList);


            ISheet GetSheetFromBook(TemplateSheetInstance dbSheet)
            {
                //var xx = Enumerable.Range(0, DestExcelBook.NumberOfSheets - 1).Select(idx => DestExcelBook.GetSheetAt(idx).SheetName);


                var sheetTabName = dbSheet.SheetTabName.Trim();
                if (dbSheet.TableID == -1)
                {

                    var newSheet = DestExcelBook.CreateSheet(sheetTabName);
                    var row = newSheet.CreateRow(0);
                    var col = row.CreateCell(0);
                    col.SetCellValue($"{dbSheet.TableCode} - Empty Table");
                    ExcelHelperFunctions.CreateHyperLink(newSheet, WorkbookStyles);
                    return newSheet;
                }

                var sheet = DestExcelBook.GetSheet(dbSheet.SheetTabName.Trim());

                return sheet;
            }
            static List<TemplateSheetInstance> getOrCreateDbSheet(ConfigObject confObj, int documentId, string? tableCode, string zetValue)
            {
                using var connectionEiopa = new SqlConnection(confObj.EiopaDatabaseConnectionString);
                using var connectionInsurance = new SqlConnection(confObj.LocalDatabaseConnectionString);

                var sqlSheetWithoutZet = @"
                    SELECT sheet.TemplateSheetId, sheet.SheetCode, sheet.TableCode,sheet.SheetTabName
                    FROM TemplateSheetInstance sheet
                    WHERE sheet.InstanceId = @documentId
                     AND sheet.TableCode= @tableCode                     
                ";

                var sqlSheetWithZet = @"
                    SELECT sheet.TemplateSheetId, sheet.SheetCode, sheet.TableCode,sheet.SheetTabName
                    FROM TemplateSheetInstance sheet
                    left outer join   SheetZetValue zet on zet.TemplateSheetId= sheet.TemplateSheetId
                    WHERE sheet.InstanceId = @documentId
                        AND sheet.TableCode= @tableCode                     
                        and zet.Dim in ('BL','OC','CR')
                        and zet.Value = @zetValue
                ";


                var sqlSheets = string.IsNullOrEmpty(zetValue) ? sqlSheetWithoutZet : sqlSheetWithZet;
                var result = connectionInsurance.Query<TemplateSheetInstance>(sqlSheets, new { documentId, tableCode, zetValue }).ToList();
                if (result.Count == 0)
                {
                    var new_sheetName = "new_" + tableCode.Trim() + "_" + zetValue.Trim();
                    new_sheetName = new_sheetName.Replace(":", "_");
                    var sheetInstance = new TemplateSheetInstance()
                    {
                        TableCode = tableCode,
                        SheetTabName = new_sheetName,
                        TableID = -1
                    };
                    var newList = new List<TemplateSheetInstance>() { sheetInstance };
                    return newList;
                }

                return result;
            }

        }

        private static List<TemplateBundle> CreateTemplateTableBundles(ConfigObject ConfObject, int moduleId)
        {
            using var connectionEiopa = new SqlConnection(ConfObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfObject.LocalDatabaseConnectionString);

            var templateTableBundles = new List<TemplateBundle>();

            var sqlTables = @"
                SELECT va.TemplateOrTableCode,va.TemplateOrTableLabel
                FROM mModuleBusinessTemplate mbt
                LEFT OUTER JOIN mTemplateOrTable va ON va.TemplateOrTableID = mbt.BusinessTemplateID
                LEFT OUTER JOIN mModule mod ON mbt.ModuleID = mod.ModuleID
                WHERE 1 = 1
                    and TemplateOrTableCode like 'S.%'
                    AND mod.ModuleID = @moduleId
                ORDER BY mod.ModuleID
                ";
            //todo make it empty list if null
            var templates = connectionEiopa.Query<mTemplateOrTable>(sqlTables, new { moduleId });



            foreach (var template in templates)
            {
                var sqlTableCodes = @"
                SELECT  tab.TableCode
                FROM mTemplateOrTable va
                LEFT OUTER JOIN mTemplateOrTable bu ON bu.ParentTemplateOrTableID = va.TemplateOrTableID
                LEFT OUTER JOIN mTemplateOrTable anno ON anno.ParentTemplateOrTableID = bu.TemplateOrTableID
                LEFT OUTER JOIN mTaxonomyTable taxo ON taxo.AnnotatedTableID = anno.TemplateOrTableID
                LEFT OUTER JOIN mTable tab ON tab.TableID = taxo.TableID
                WHERE 1 = 1
                    AND va.TemplateOrTableCode = @templateCode
                ORDER BY tab.TableCode

                ";
                var tableCodes = connectionEiopa.Query<string>(sqlTableCodes, new { templateCode = template.TemplateOrTableCode })?.ToList() ?? new List<string>();
                templateTableBundles.Add(new TemplateBundle(template.TemplateOrTableCode, template.TemplateOrTableLabel, tableCodes));

                //var sheets= connectionInsurance.Query<TemplateSheetInstance>(sqlSheets, new { documentId,bCode }).ToList()?? new List<TemplateSheetInstance>();
                //TemplateCodes.Add(new BusinessTableBundle(tableCode, sheets));                



            }
            return templateTableBundles;

        }

    }
}