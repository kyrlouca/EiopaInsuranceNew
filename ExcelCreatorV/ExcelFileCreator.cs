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

namespace ExcelCreatorV
{
    internal enum LineState { Start, SheetCodeState, ZetState, ColumnState, RowState, ErrorState, EndState };
    internal enum LineType { Empty, AnyText, SheetCode, Zet, Column, Row }//Code is template code "PF.01.01.02"

    internal readonly record struct MergedSheetRecord
    {
        public ISheet TabSheet { get; init; }
        public List<TemplateSheetInstance> ChildrenSheetInstances { get; init; }
        public string SheetDescription { get; init; }
        public MergedSheetRecord(ISheet tabSheet, string sheetDescription, List<TemplateSheetInstance> childrenSheetInstances)
        {
            TabSheet = tabSheet;
            SheetDescription = sheetDescription;
            ChildrenSheetInstances = childrenSheetInstances;
        }
    }

    internal readonly record struct TemplateTableBundle
    {
        public string TemplateTableCode { get; init; }
        public string TemplateDescription { get; init; }
        public List<String> TableCodes { get; init; }        
        public TemplateTableBundle( string templateTableCode, string templateDescription, List<string> tableCodes)
        {
            TemplateTableCode = templateTableCode;
            TemplateDescription = templateDescription;
            TableCodes = tableCodes ;
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
            if (ConfigObject is null)
            {
                var errMessage = $"Cannot create ConfigObject";
                Console.WriteLine(errMessage);
                return false;
            }

            Document = GetDocumentById2(DocumentIdInput);
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

            var businessTables = CreateBusinessTableBundles(ConfigObject, ModuleId);
            return;


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

            var IndexListSheet = new IndexSheetList(ConfigObject, DestExcelBook, WorkbookStyles, sheets, "List", "List of Templates");

            //---------------------------------------------------------------
            //Create a sheet which combines S0.06.02.01.01 with S0.06.02.01.02
            var sheetS06Name = "S.06.02.01 Combined";
            var sheetS06Combined = new SheetS0601Combined(ConfigObject, DestExcelBook, sheetS06Name, WorkbookStyles);
            sheetS06Combined.CreateS06CombinedSheet();
            if (!sheetS06Combined.IsEmpty)
            {
                IndexListSheet.AddSheet(new IndexSheetListItem("S.06.02.01 Combined", "List of assets ## Information on positions held ##  Information on assets"));
            }

            //for each value of the Bl dim in S.19.01.01, we create a merged Sheet which contains the associated s19.01.01.xx sheets            
            var bl19MergedSheets = MergeAllS1901("S.19.01.01", "BL");

            foreach (var bl19MergedSheet in bl19MergedSheets)
            {
                var sheetNamesToDelete = bl19MergedSheet.ChildrenSheetInstances.Select(sheet => sheet.SheetTabName.Trim()).ToList();
                var ss = sheetNamesToDelete
                    .Select(name => DestExcelBook.GetSheet(name)).ToList();

                IndexListSheet.RemoveSheets(sheetNamesToDelete);
                IndexListSheet.AddSheet(new IndexSheetListItem(bl19MergedSheet.TabSheet.SheetName, bl19MergedSheet.SheetDescription));
            }


            var S05 = MergeS05_02_01("S.05.02.01", "Premiums, claims and expenses by country");

            if (S05.TabSheet is not null)
            {
                var S05SheetsToRemove = S05.ChildrenSheetInstances.Select(sheet => sheet.SheetTabName.Trim()).ToList();
                IndexListSheet.RemoveSheets(S05SheetsToRemove);
                IndexListSheet.AddSheet(new IndexSheetListItem(S05.TabSheet.SheetName, S05.SheetDescription));
            }


            //*******************************************
            IndexListSheet.Sort();
            IndexListSheet.PrepareIndexSheet();
            IndexListSheet.IndexSheet.SetZoom(80);

            DestExcelBook.SetSheetOrder("List", 0);
            DestExcelBook.SetActiveSheet(0);


        }

        private List<MergedSheetRecord> MergeAllS1901(string tableCode, string dimPivot)
        {

            using var connectionLocalDb = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var mergedSheets = new List<MergedSheetRecord>();

            var tableCodeLike = $"{tableCode}%";


            var sqlBlValues = @"
                    select 
	                    sz.Value
                    from 
	                    SheetZetValue sz 
	                    join TemplateSheetInstance sheet on sheet.TemplateSheetId= sz.TemplateSheetId
                    where 1=1 
	                    and sheet.InstanceId= @documentId
	                    and sheet.TableCode like @tableCodeLike
	                    and sz.Dim=@dimPivot
                    group by sz.Dim, sz.Value

                    ";
            var dimBLValues = connectionLocalDb.Query<string>(sqlBlValues, new { DocumentId, tableCodeLike, dimPivot });
            foreach (var dimBLValue in dimBLValues)
            {
                var mergedSheet = MergeOneBL_S1901010(tableCode, dimPivot, dimBLValue);
                mergedSheet.TabSheet.SetZoom(80);
                mergedSheets.Add(mergedSheet);
            }

            var affectedSheets = new List<TemplateSheetInstance>();

            return mergedSheets;

        }

        private MergedSheetRecord MergeOneBL_S1901010(string tableCodeS19, string dimPivot, string blDimValue)
        {
            using var connectionLocalDb = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var blList = new List<List<ISheet>>();
            //create a sheet for each *BL* dim value 
            //the merged sheet consist of pairs stacked 1st pair is 'S.19.01.01.01, S.19.01.01.02' second pair is 'S.19.01.01.03, S.19.01.01.04', etc.


            var sqlSheetsWithBL = @"
                    select 
	                    sheet.TableCode, sheet.SheetCode,SheetTabName, sheet.TemplateSheetId
                    from 
	                    SheetZetValue sz 
	                    join TemplateSheetInstance sheet on sheet.TemplateSheetId= sz.TemplateSheetId
                    where 1=1 
	                    and sheet.InstanceId= @documentId
	                    and sheet.TableCode like 'S.19.01.01.%'
	                    and sz.Dim='BL'
	                    and sz.Value=@blDimValue
                    ";
            var blSheets = connectionLocalDb.Query<TemplateSheetInstance>(sqlSheetsWithBL, new { DocumentId, blDimValue }).ToList();


            //find the  sheets  with odd table code
            var blSheetsOdd = blSheets
                .Where(sheet => OddTableCodeSelector(sheet.TableCode));

            //will also get the corresponding sheets with the even tablecode
            foreach (var blSheetOdd in blSheetsOdd)
            {
                //create pairs of sheets and add the pair to the blList
                var oddSheet = DestExcelBook.GetSheet(blSheetOdd.SheetTabName.Trim());
                var pairList = new List<ISheet>() { oddSheet };

                var evenSheetTableCode = ModifyTableCode(blSheetOdd.TableCode.Trim());
                var evenSheetDb = blSheets.FirstOrDefault(sheet => sheet.TableCode == evenSheetTableCode);
                if (evenSheetDb != null)
                {
                    var evenSheet = DestExcelBook.GetSheet(evenSheetDb?.SheetTabName?.Trim());
                    pairList.Add(evenSheet);
                }

                blList.Add(pairList);
            }

            //**********************************************
            //Create the Merged Sheet  for table s.19.01.01          

            var mergedSheetName = $"{tableCodeS19}#{blDimValue.Split(":")[1].Trim()}";
            var mergedDimValueDescription = GetDimValueDescription(ConfigObject, blDimValue);

            var sheetCreated = CreateMergedSheet(blList, mergedSheetName);
            ExcelHelperFunctions.CreateHyperLink(sheetCreated, WorkbookStyles);

            //sheetCreated.SetColumnHidden(18, true);
            //sheetCreated.SetColumnHidden(19, true);


            return new MergedSheetRecord(sheetCreated, mergedDimValueDescription, blSheets);
            //**********************************************

            static bool OddTableCodeSelector(string tableCode)
            {
                //retruns true if last part of tablecode is odd // "S.19.01.01.05"=> true because "05" is odd
                var match = RegexConstants.TableCodeRegExP.Match(tableCode);
                if (match.Success)
                {
                    // "S.19.01.01.05"=> "05"
                    var lastDigits = match.Groups[2].Captures
                        .Select(cpt => cpt.Value[1..])
                        .ToArray()[3];

                    return int.Parse(lastDigits) % 2 != 0;

                }
                return false;
            }


            static string ModifyTableCode(string tableCode)
            {
                //retruns true if last part of tablecode is odd // "S.19.01.01.05"=> true because "05" is odd
                var match = RegexConstants.TableCodeRegExP.Match(tableCode);
                if (match.Success)
                {
                    // "S.19.01.01.05"=> "05"
                    var lastDigits = match.Groups[2].Captures
                        .Select(cpt => cpt.Value[1..])
                        .ToArray();

                    var incDigit = int.Parse(lastDigits[3]) + 1;
                    var modCode = $"{match.Groups[1].Value}.{lastDigits[0]}.{lastDigits[1]}.{lastDigits[2]}.{incDigit:D2}";
                    return modCode;
                }
                return "";
            }

            static string GetDimValueDescription(ConfigObject confObject, string dimValue)
            {
                using var connectionEiopa = new SqlConnection(confObject.EiopaDatabaseConnectionString);
                var sqlDimValue = "select MemberLabel from mMember mem where mem.MemberXBRLCode=@dimValue";
                var res = connectionEiopa.QueryFirstOrDefault<MMember>(sqlDimValue, new { dimValue });
                return res is null ? "" : res.MemberLabel;
            }

        }



        private MergedSheetRecord MergeS05_02_01(string mergedSheetTabName, string mergedSheetDescription)
        {
            using var connectionLocalDb = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var blList = new List<List<ISheet>>();

            //the merged sheet consist of sheets 'S.15.02.01.xx, where xx is 01,02,03

            var sqlSheets = @"
                   select sheet.TableCode ,sheet.TemplateSheetId,sheet.SheetTabName  from TemplateSheetInstance sheet 
                    where sheet.InstanceId= @documentId
                    and sheet.TableCode in ('S.05.02.01.01','S.05.02.01.03','S.05.02.01.02')
                    ";
            var dbSheets = connectionLocalDb.Query<TemplateSheetInstance>(sqlSheets, new { DocumentId }).ToList();
            if (!dbSheets.Any())
            {
                return new MergedSheetRecord();
            }


            var iUnsortedSheets = dbSheets.Select(dbSheet => DestExcelBook.GetSheet(dbSheet.SheetTabName.Trim())).ToList();
            var iSheets = new List<ISheet>();

            var s1 = iUnsortedSheets.FirstOrDefault(dbSheet => dbSheet.SheetName.Trim() == "S.05.02.01.01");
            if (s1 is not null) iSheets.Add(s1);

            var s3 = iUnsortedSheets.FirstOrDefault(dbSheet => dbSheet.SheetName.Trim() == "S.05.02.01.03");
            if (s3 is not null) iSheets.Add(s3);

            var s2 = iUnsortedSheets.FirstOrDefault(dbSheet => dbSheet.SheetName.Trim() == "S.05.02.01.02");
            if (s2 is not null) iSheets.Add(s2);

            blList.Add(iSheets);


            //**********************************************
            //Create the Merged Sheet             
            var mergedSheetCreated = CreateMergedSheet(blList, mergedSheetTabName);

            mergedSheetCreated.SetZoom(80);
            if (s3 is not null)
            {
                mergedSheetCreated.SetColumnHidden(3, true);
                mergedSheetCreated.SetColumnHidden(4, true);
            }
            if (s2 is not null && s3 is not null)
            {
                mergedSheetCreated.SetColumnHidden(6, true);
                mergedSheetCreated.SetColumnHidden(7, true);
            }



            ExcelHelperFunctions.CreateHyperLink(mergedSheetCreated, WorkbookStyles);
            return new MergedSheetRecord(mergedSheetCreated, mergedSheetDescription, dbSheets);

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

            destSheet.SetColumnWidth(0, 12000);
            destSheet.SetColumnWidth(1, 2000);
            for (var j = 2; j < firstRow?.Cells.Count; j++)
            {
                destSheet.SetColumnWidth(j, 4000);
            }
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


        public DocInstance GetDocumentById2(int documentId)
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

        private static List<TemplateTableBundle> CreateBusinessTableBundles(ConfigObject ConfObject,  int moduleId)
        {
            using var connectionEiopa = new SqlConnection(ConfObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfObject.LocalDatabaseConnectionString);

            var templateTableBundles = new List<TemplateTableBundle>();

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



            foreach(var template in templates)
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
                var tableCodes = connectionEiopa.Query<string>(sqlTableCodes, new { templateCode=template.TemplateOrTableCode })?.ToList()??new List<string>();
                templateTableBundles.Add(new TemplateTableBundle(template.TemplateOrTableCode, template.TemplateOrTableLabel, tableCodes));
                //var sqlSheets = @"
                //    SELECT sheet.TemplateSheetId, sheet.SheetCode, sheet.TableCode
                //    FROM TemplateSheetInstance sheet
                //    WHERE sheet.InstanceId = @documentId
	               //     AND sheet.TableCode LIKE @bCode";
                //var bCode = $"{tableCode}%";
                
                //var sheets= connectionInsurance.Query<TemplateSheetInstance>(sqlSheets, new { documentId,bCode }).ToList()?? new List<TemplateSheetInstance>();
                //TemplateCodes.Add(new BusinessTableBundle(tableCode, sheets));                



            }
            return templateTableBundles;

        }
        
        public void MergeBusinessTables()
        {
            var btList = CreateBusinessTableBundles(ConfigObject, ModuleId);
            foreach(var btBundle in btList)
            {
                //var mergedSheetName = $"{btBundle.TableCode}";
                //var excelSheets = btBundle.SheetInstances.Select(sheet => DestExcelBook.GetSheet(sheet.SheetTabName.Trim()));                               
                //var sheetCreated = CreateMergedSheet(excelSheets, mergedSheetName);
            }
            
            

        }

    }
}