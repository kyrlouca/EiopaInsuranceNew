using ConfigurationNs;
using Dapper;
using EntityClasses;
using GeneralUtilsNs;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;
using HelperInsuranceFunctions;
using TransactionLoggerNs;

namespace XbrlReader
{


    class AssignFactsToSheets
    {        
        public int TestingTableId { get; set; } = 0;
        //65 S.05.02.01.02 ars error fin grawo
        //60 S.05.01.02.01  qrs simple zet
        //39 - S.02.02.01.01 ars  simple zet          
        //1-"S.01.02.01.01"
        //150=S.21.01.01.01
        // 124= S.19.01.01.01  ars complex z
        // 125= S.19.01.01.02  xx
        //30 - S.02.01.02.01 simple shaded cells
        //71 -S.06.02.01.01
        //40  - S.02.02.01.02   multiple currencys altius yearly

        //public int TestingTableId { get; set; } = 125,489;105;

        public DateTime StartTime { get; } = DateTime.Now;

        public int PensionFundId { get; private set; }
        public int ApplicableYear { get; private set; }
        public int ApplicableQuarter { get; private set; }
        public int UserId { get; private set; }
        public int FileName { get; private set; }

        public int DocumentId { get; private set; }
        public DocInstance Document { get; set; }
        public List<string> Filings { get; private set; }
        public string DefaultCurrency { get; set; } = "EUR";
        public int ModuleId { get; private set; }
        public string ModuleCode { get; private set; }        
        public List<MTable> ModuleTablesFiled { get; private set; } = new List<MTable>();
        

        public string SolvencyVersion { get; private set; }
        public ConfigObject ConfigObject { get; }

        //n


        public AssignFactsToSheets(string solvencyVersion, int documentId, List<string> filings)
        {
            //process all the tables (S.01.01.01.01, S.01.01.02.01, etc ) related to the filings (S.01.01)
            //for each cell in each table, create a sheet and associate the mathcing facts (or create new facts if a fact should be in two tables)            
            //for open tables, create  facts for the Y columns in each row based on rowContext

            Console.WriteLine("Starting XbrlDataProcessor");
            SolvencyVersion = solvencyVersion;
            ConfigObject = Configuration.GetInstance(SolvencyVersion).Data;
            if (string.IsNullOrEmpty(ConfigObject.EiopaDatabaseConnectionString) || string.IsNullOrEmpty(ConfigObject.LocalDatabaseConnectionString))
            {
                var errorMessage = "Empty ConnectionStrings";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }

            DocumentId = documentId;
            if (documentId == 0)
            {
                var message = $"Document id:{0}. Abort";
                Console.Write(message);
                Log.Error(message);
            }

            Document = GetDocument(documentId);
            if (Document is null)
            {
                var message = $"Document id:{DocumentId} NOT Found";
                Console.Write(message);
                Log.Error(message);
                
                var trans = new TransactionLog()
                {
                    PensionFundId = 0,
                    ModuleCode = "",
                    ApplicableYear = 0,
                    ApplicableQuarter =0,
                    Message = message,
                    UserId = UserId,
                    ProgramCode = ProgramCode.RX.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = 0,
                    MessageType = MessageType.ERROR.ToString()
                };
                TransactionLogger.LogTransaction(SolvencyVersion, trans);
                return;
            }


            Filings = filings;

            ModuleCode = Document.ModuleCode.Trim();

            //Testing1();
            //return; //xx
            Console.WriteLine($"\n Database Writer Started");

            ModuleTablesFiled = GetFiledModuleTables();                        
            var countFacts = ProcessModuleTables();

            UpdateDocumentStatus("L");

            Log.Information($"DocId:{DocumentId} DataProcessor time:{StartTime.Subtract(DateTime.Now).TotalSeconds}, facts :{countFacts}");
            Console.WriteLine($"\ndocId: {DocumentId} -- sheets: facts:{countFacts}");
        }

        private void UpdateDocumentStatus(string status)
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqlUpdate = @"update DocInstance  set status= @status where  InstanceId= @documentId;";
            var doc = connectionInsurance.Execute(sqlUpdate, new { DocumentId,status });
        }

        private DocInstance GetDocument(int documentId)
        {
            var sqlGetDocument = @"
                    SELECT
                      doc.InstanceId
                     ,doc.PensionFundId
                     ,doc.ModuleId
                     ,doc.Status
                     ,doc.ModuleCode
                     ,doc.ApplicableYear
                     ,doc.ApplicableQuarter
                     ,doc.EntityCurrency
                     ,doc.UserId
                    FROM dbo.DocInstance doc
                    WHERE doc.InstanceId = @documentId
                    ";
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var doc = connectionInsurance.QuerySingleOrDefault<DocInstance>(sqlGetDocument, new { documentId });
            return doc;
        }


        public static void TestingCode(ConfigObject config)
        {
            using var connectionInsurance = new SqlConnection(config.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(config.EiopaDatabaseConnectionString);
            var sqlx = "select tab.TableID from mTable tab where tab.TableID =222333";
            var xtab = connectionEiopa.Query<MTable>(sqlx).ToList();
            var list = new List<int>() { 1, 3, 5 };
            var l = list.Where(item => item > 22).ToList();
            return;
        }

        private int ProcessModuleTables()
        {
            //iterate each table. For each table, read its cells, find the matching facts, and assign them rowcols
            Console.WriteLine($"\nCreating facts");
            var count = 0;

            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            if (TestingTableId > 0)
            {
                ModuleTablesFiled = ModuleTablesFiled.Where(table => table.TableID == TestingTableId).ToList();
            }

            //ModuleTablesFiled = ModuleTablesFiled.Where(table => table.TableID == 29 || table.TableID == 39).ToList();





            foreach (var table in ModuleTablesFiled.OrderBy(tab => tab.TableID))
            {

                //A table may result in many sheets, facts for the table may have different Z values.
                //if there is an open Z(*) for a table, one or more sheets will be created (one for each Z)
                //each z sheet will have its own facts with the same row/col 
                //In the same sheet, A cell can still result in more than one fact, if multicurrency. In this, case create another column when creating excel
                Console.WriteLine($"\nTable start : {table.TableCode}");

                //************************************************************************
                var factCount = AssignFactsToTableDb(table);
                //************************************************************************

                UpdateSheetTabNames(table.TableCode); //make the tabnames simler to read

                count += factCount;
                Console.WriteLine($"\n---facts:{factCount}");
            }

            //Create the Y facts fore open tables
            var sqlSelectSheets = @"select sheet.TemplateSheetId from TemplateSheetInstance sheet where sheet.InstanceId = @documentId";
            var sheets = connectionInsurance.Query<TemplateSheetInstance>(sqlSelectSheets, new { DocumentId })?.ToList() ?? new();
            foreach (var sheet in sheets)
            {
                var yFactsCounter = CreateYFactsInDb(sheet.TemplateSheetId);
                Console.WriteLine($"\n---Yfacts:{yFactsCounter}");
            }

            return count;
        }


        private int AssignFactsToTableDb(MTable table)
        {
            //Iterate through all the cells of the table and find the facts for each cell           
            // For each cell of the table
            // -- find the corresponding  xbuFacts  (one cell may correspond to many facts -- one for each  Z or even for fact zet such as currency)
            // -- find the corresponding MAPPING which has the rowcol
            // -- create the TemplateSheetFacts in the DB            
            // create one sheet for each new Zet found in the table's facts.


            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            DeleteMappingSignature(DocumentId);

            var factCounter = 0;

            var isOpenTable = CreateMappingsSignaturesDb(table);
            table.IsOpenTable = isOpenTable;  //i dont' like this but I didn't like to add an attribute on an eiopa table
            var pivotZet = GetTablePivotZet(ConfigObject, table);  //the dim for multifact cells                                            


            var sqlCells = @"SELECT
                      cell.CellID
                     ,cell.TableID
                     ,cell.IsRowKey
                     ,cell.IsShaded
                     ,cell.BusinessCode
                     ,cell.DatapointSignature
                     ,cell.DPS
                     ,cell.NoOpenDPS
                     ,IsShaded
                    FROM dbo.mTableCell cell
                    WHERE cell.TableID = @TableId
                    and cell.isShaded=0
                    ";
            var cells = connectionEiopa.Query<MTableCell>(sqlCells, new { table.TableID });
            Console.Write($"cells:{cells.Count()}-");

            foreach (var cell in cells)
            {
                if (string.IsNullOrEmpty(cell.DatapointSignature))
                {
                    continue;
                }

                //****************************************************************************************
                //find the mapping which provides the row/col  corresponding to the cell. Fact is not involved                
                //****************************************************************************************
                //Console.Write($"m");
                var cellMappingNew = FindMappingRowColForTheCell(table, cell);


                if (string.IsNullOrWhiteSpace(cellMappingNew.DYN_TAB_COLUMN_NAME))
                {
                    var message = $"Cannot find mappings for Cell: {cell.CellID}";
                    Log.Error(message);

                    continue;
                }


                //*****************************************************************
                //** One Or More Facts for each cell- because of sheet zet or fact zet such as currency, country, etc
                //** find the facts matching the the Cell's signature. 
                //** the fact signature has REAL values (From its context) and the cells may have wildcard values                        
                //*****************************************************************
                Console.Write($"!");


                //var factsList = FindMatchingFactsV5(ConfigObject, DocumentId, cell.DatapointSignature);
                var factsList = FindMatchingFactsRegex(ConfigObject, DocumentId, cell.DatapointSignature);
                //if (factsList.Count != testList.Count)
                //{
                //    var x = 3;
                //}

                Console.Write($"$");

                if (factsList.Count == 0)
                {
                    continue;
                }

                foreach (var foundFact in factsList)
                {
                    var fact = foundFact;
                    //update the fact open Zet dims (rowcol,currency, country,etc)
                    Console.Write($".");
                    if (fact.TemplateSheetId > 0)
                    {
                        //the fact belongs ALSO in another sheet. Create a new ONE  //@ here is my problem: if we had two previous facts with the same signature we will create two more. but i need to check the zet
                        //new Fact has its own factId, SheetId, AND tableId
                        fact.TableID = table.TableID;
                        fact.CellID = cell.CellID;
                        if (table.IsOpenTable)
                        {
                            fact.Row = "";
                            fact.InternalRow = 0;
                        }
                        fact.FieldOrigin = "KYR";
                        //do NOT use fields  templateSheetId and factId, and tableId  is new 
                        var sqlInsertAnotherFact = @"
                            INSERT INTO dbo.TemplateSheetFact (  Row, Col, Zet, CellID, FieldOrigin, TableID, DataPointSignature, Unit, Decimals, NumericValue, BooleanValue, DateTimeValue, TextValue, DPS, IsRowKey, IsShaded, XBRLCode, DataType, DataPointSignatureFilled,  InternalRow, internalCol, DataTypeUse, IsEmpty, IsConversionError, ZetValues, OpenRowSignature, CurrencyDim,  metricId, contextId,  RowSignature,  InstanceId)
                            VALUES (  @Row, @Col, @Zet, @CellID, @FieldOrigin, @TableID, @DataPointSignature, @Unit, @Decimals, @NumericValue, @BooleanValue, @DateTimeValue, @TextValue, @DPS, @IsRowKey, @IsShaded, @XBRLCode, @DataType, @DataPointSignatureFilled,  @InternalRow, @internalCol, @DataTypeUse, @IsEmpty, @IsConversionError, @ZetValues, @OpenRowSignature, @CurrencyDim,  @metricId,  @contextId,  @RowSignature, @InstanceId);
                            SELECT CAST(SCOPE_IDENTITY() as int);
                        ";

                        var factId = connectionInsurance.QueryFirst<int>(sqlInsertAnotherFact, fact);
                        fact.FactId = factId;
                        
                        XbrlFileReader.CreateFactDimsDb(ConfigObject, fact.FactId, fact.DataPointSignature);


                    }
                    var sheetFact = UpdateFactWithCellValuesInDb(table, cell, cellMappingNew, fact, pivotZet);
                    factCounter += 1;
                }

            }
            return factCounter;
        }


        static List<string> GetTableYDims(MTable table)
        {
            //these are the dims that will be added on MAPPINGS to make a mapping signature
            //var zDimsAll = table.ZDimVal?.Split("|")?.ToList() ?? new List<string>(); //apply to all mappings                        
            //var zDimsClosed = zDimsAll.Where(item => !item.Contains("(*")).ToList() ?? new List<string>();//s2c_dim:LG(*[GA_18;x0;0])=> 
            //var zDimsOpen = zDimsAll.Where(item => item.Contains("(*"))
            //    .Select(dim => Regex.Replace(dim, @"\(\*(.*?)\)", "(*)")).ToList();  //s2c_dim:LG(*[GA_18;x0;0])=>s2c_dim:LG(*)

            var yDimsAll = table.YDimVal?.Split("|")?.ToList() ?? new List<string>();// apply to all  in cells in the row            

            var yDimsClosed = yDimsAll.Where(item => !item.Contains("(*")).ToList() ?? new List<string>();
            var yDimsOpen = yDimsAll.Where(item => item.Contains("(*"))
                .Select(dim => Regex.Replace(dim, @"\(\*(.*?)\)", "(*)")).ToList();  //s2c_dim:LG(*[GA_18;x0;0])=>s2c_dim:LG(*)

            var tableDims = new List<string>();

            tableDims.AddRange(yDimsOpen);
            tableDims.AddRange(yDimsClosed);


            return tableDims;
        }


        private bool CreateMappingsSignaturesDb(MTable table)
        {

            //create a list with unique rowCol (dyn_tab_column_name) for the table
            //for each rowCol concatenate its dims
            //---- use the function STRING_AGG which concatenates values from different rows -- makes life much easier
            // Take the Y dims from the table ydimsVal
            // However, we cannot take the zet from the table zdimsVal since some are missing
            // we get the Z from the mappings
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);


            var yMappings = GetTableYDims(table);
            if (yMappings.Count > 0)
            {             
            }


            var sqlFieldMappings = @"
                SELECT 
                    DYN_TAB_COLUMN_NAME, 
                    STRING_AGG(cast(DIM_CODE as nvarchar(1000)), '|') as Dims
                FROM MAPPING map
                where map.TABLE_VERSION_ID=@tableId
                and map.DYN_TAB_COLUMN_NAME not like 'PAGE%'
                GROUP BY 
                    DYN_TAB_COLUMN_NAME
                ";
            var fieldMappings = connectionEiopa.Query<(string rowCol, string dims)>(sqlFieldMappings, new { tableId = table.TableID })?.ToList() ?? new List<(string rowCol, string dims)>();
            var isOpenTable = fieldMappings?.Any(item => item.rowCol.StartsWith("C")) ?? false;

            //dims outside the table as Zet 
            var sqlOutOfTabl = @"
                        select map.DIM_CODE from MAPPING map where 
                        map.ORIGIN='C'
                        and map.IS_IN_TABLE=0
                        and map.TABLE_VERSION_ID =@tableId
                        ";
            var zetTableDims = connectionEiopa.Query<string>(sqlOutOfTabl, new { table.TableID })?.ToList() ?? new List<string>();

            //column dims inside the table
            var sqlAllInsideTable = @"
                select map.DIM_CODE from MAPPING map where 
                map.ORIGIN='C'
                and map.IS_IN_TABLE=1
                and map.IS_PAGE_COLUMN_KEY=1
                and map.TABLE_VERSION_ID = @tableId
                ";
            var allInsideTableDims = connectionEiopa.Query<string>(sqlAllInsideTable, new { table.TableID })?.ToList() ?? new List<string>();


            foreach (var (rowCol, dims) in fieldMappings)
            {

                var fieldDims = dims.Split("|", StringSplitOptions.RemoveEmptyEntries)?.ToList() ?? new List<string>();
                ///////////////////////

                var fullMappings = new List<string>();
                fullMappings.AddRange(fieldDims);
                fullMappings.AddRange(zetTableDims);
                fullMappings.AddRange(allInsideTableDims);
                if (isOpenTable)
                {
                    fullMappings.AddRange(yMappings);
                }
                fullMappings.Sort();
                var fulldims = string.Join("|", fullMappings);


                var sqlInsertMappingSig = @"
                        INSERT INTO dbo.MappingSignatures (InstanceId, tableId, Signature, RowCol ,isOpenTable) VALUES (@InstanceId, @tableId, @Signature, @RowCol, @isOpenTable)
                        SELECT CAST(SCOPE_IDENTITY() as int);                            
                    ";

                var mappingSignature = new MappingSignatures(DocumentId, table.TableID, fulldims, rowCol, isOpenTable);
                var mapp = connectionInsurance.QueryFirstOrDefault<int>(sqlInsertMappingSig, mappingSignature);
            }

            return isOpenTable;
        }

        TemplateSheetFact UpdateFactWithCellValuesInDb(MTable table, MTableCell cell, MAPPING cellMapping, TemplateSheetFact realFact, string tablePivotZet)
        {
            //update the fact with row,col, cellId and assign it to a sheet (or create a new one if does not exist)
            //we create a sheet when the fact cannot find another fact with similar Zet values
            //fact zet values are the zet dims present in the fact signature
            //they tell us when the same facts belong in different sheets either bucause of
            //-- different open values which means multiple sheets of the same tableI
            //-- or different closed valued (completely different table)

            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            realFact.CellID = cell.CellID;
            realFact.TableID = table.TableID;

            var factRowCol = cellMapping.DYN_TAB_COLUMN_NAME;
            realFact.Col = GeneralUtils.GetRegexSingleMatch(@"(C\d*)", factRowCol);
            realFact.Row = GeneralUtils.GetRegexSingleMatch(@"(R\d*)", factRowCol);
            realFact.IsRowKey = (cellMapping.IS_PAGE_COLUMN_KEY == 1);

            //the fact.RowSignature was already builted when reading the element from xbrl 
            //but   OpenRowSignature cannot be builted when reading the xbrl file because  we need the table and the cell             
            realFact.OpenRowSignature = GetWildDims(realFact.DataPointSignatureFilled, table.YDimVal);

            //factZetvalues are all the z dimensions of the fact, which are also part of the sheetcode name            
            var factZetValuesList = ConstructFactFullZetList(realFact.DataPointSignatureFilled, table.ZDimVal);
            var factZetValuesStr = string.Join("__", factZetValuesList);
            realFact.ZetValues = factZetValuesStr;

            //fact.zet is the zet dimension of the fact, in case a cell corresponds to more than one fact in the same sheet (currency, etc)
            var factPivotZet = GetFactPivotZet(realFact.DataPointSignatureFilled, tablePivotZet);
            var pivotZetDimDom = DimDom.GetParts(factPivotZet);
            //realFact.Zet = string.IsNullOrEmpty(factPivotZet) ? "" : $"{pivotZetDimDom.Dim}#{pivotZetDimDom.DomAndValFull }";
            realFact.Zet = factPivotZet ?? "";
            realFact.CurrencyDim = pivotZetDimDom.Dim == "OC" ? pivotZetDimDom.Dom : DefaultCurrency;

            //assing the fact to a new or an existing sheet based on its zetValues
            var sheet = GetOrCreateSheet(table, realFact);
            realFact.TemplateSheetId = sheet.TemplateSheetId;

            var sqlUpdFact = @"
                UPDATE dbo.TemplateSheetFact
                SET 
                    TemplateSheetId = @TemplateSheetId
                    ,Row = @Row
                    ,InternalRow=@InternalRow
                    ,Col = @Col
                    ,Zet = @Zet
                    ,CellID = @CellID
                    ,TableID = @TableID
                    ,IsRowKey = @IsRowKey                    
                    ,ZetValues=@ZetValues
                    ,CurrencyDim = @CurrencyDim                    
                    ,OpenRowSignature = @OpenRowSignature                   
                WHERE FactId = @factId

            ";

            if (string.IsNullOrEmpty(realFact.Row))
            {

                //Only open tables have a column but NO row. 
                //Assign a row number if this is the first fact with that rowSignature (does not include xbrlcode) 

                var openRowNumber = GetOrUpdateOpenRowNumber(sheet, realFact);
                realFact.InternalRow = openRowNumber;
                realFact.Row = $"R{openRowNumber:D4}";
            }

            connectionInsurance.Execute(sqlUpdFact, realFact);
            return realFact;
        }

        private static string GetFactPivotZet(string factSignature, string zetDim)
        {
            if (string.IsNullOrEmpty(zetDim))
            {
                return "";
            }
            var tableZetDim = DimDom.GetParts(zetDim).Dim;
            var factDims = factSignature.Split("|")?.Where(dim => DimDom.GetParts(dim).Dim.Contains(tableZetDim))?.ToList();
            if (factDims.Count == 0)
            {
                return "";
            }
            return factDims.First();

        }

        private TemplateSheetInstance GetOrCreateSheet(MTable table, TemplateSheetFact fact)
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var sheetCode = string.IsNullOrEmpty(fact.ZetValues)
                    ? table.TableCode
                    : $"{table.TableCode}__{fact.ZetValues}";

            //find an existing  sheet based on sheetCode = tableCode and ZetValues 
            var sqlSelSheet = @"select TemplateSheetId, SheetCode,TableCode, OpenRowCounter from TemplateSheetInstance sheet where sheet.InstanceId=@documentId and  SheetCode =@sheetCode";
            var sheet = connectionInsurance.QuerySingleOrDefault<TemplateSheetInstance>(sqlSelSheet, new { DocumentId, sheetCode });

            if (sheet is null)
            {
                sheet = CreateSheet(table, sheetCode);
            }
            //var sheetId = sheet.TemplateSheetId;
            return sheet;
        }

        private int GetOrUpdateOpenRowNumber(TemplateSheetInstance sheet, TemplateSheetFact fact)
        {
            //update the rowNumber for open rows for the sheet.
            // --Use the rowNumber of another fact which has a similar openRowSignature
            // --Or use the sheet.openRowCounter to get a new row number
            //add the tableCode in front of the openRowSignature to have different rowSignatures for each Z sheet
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sheetId = sheet.TemplateSheetId;
            var sqlSameRowFact = @" SELECT fact.FactId, fact.Row, fact.InternalRow from TemplateSheetFact fact where fact.TemplateSheetId = @sheetId and fact.OpenRowSignature=@OpenRowSignature ";
            var sameRowFact = connectionInsurance.QueryFirstOrDefault<TemplateSheetFact>(sqlSameRowFact, new { sheetId, fact.OpenRowSignature });

            var openRowNumber = sameRowFact?.InternalRow ?? 0;
            if (sameRowFact is null)
            {
                openRowNumber = sheet.OpenRowCounter + 1; ;
                var sqlUpdateCounter = @"update TemplateSheetInstance set OpenRowCounter = @openRowNumber where TemplateSheetId = @sheetId";
                connectionInsurance.Execute(sqlUpdateCounter, new { sheetId, openRowNumber });
            }
            return openRowNumber;
        }

        TemplateSheetInstance CreateSheet(MTable table, string sheetCode)
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var SqlInsertTemplateSheet = @"
                         INSERT INTO TemplateSheetInstance
                           (
                            [InstanceId]
                           ,[UserId]         
                           ,[TableId]
                           ,[DateCreated]
                           ,[SheetCode]                 
                           ,[TableCode]                 
                           ,[YDimVal]
                           ,[ZDimVal]
                           ,[status]
                           ,[Description]
                           ,[XbrlFilingIndicatorCode]
                            ,[IsOpenTable]

                            )
                        VALUES
                           (
                            @InstanceId
                           ,@UserId  
                           ,@TableId
                           ,@DateCreated
                           ,@SheetCode
                           ,@TableCode
                           ,@YDimVal
                           ,@ZDimVal
                           ,@status           
                           ,@Description
                           ,@XbrlFilingIndicatorCode
                            ,@IsOpenTable
                            );        
                            SELECT CAST(SCOPE_IDENTITY() as int);
                        ";
            Console.Write(',');

            var sheet = new TemplateSheetInstance()
            {
                InstanceId = DocumentId,
                UserId = "KK",
                TableID = table.TableID,
                TableCode = table.TableCode,
                DateCreated = DateTime.Now,
                SheetCode = sheetCode,
                YDimVal = table.YDimVal,
                ZDimVal = table.ZDimVal,
                Status = "LD",
                Description = GeneralUtils.TruncateString(table.TableLabel, 199),
                XbrlFilingIndicatorCode = table.XbrlFilingIndicatorCode,
                IsOpenTable = table.IsOpenTable,
                OpenRowCounter = 0
            };

            var sheetId = connectionInsurance.QuerySingle<int>(SqlInsertTemplateSheet, sheet);
            sheet.TemplateSheetId = sheetId;


            var dims = sheetCode.Split("__");
            foreach (var factDim in dims)
            {
                var zetParts = factDim.Split("#").ToList();
                if (zetParts.Count == 2)
                {
                    var sqlZet = @"INSERT INTO SheetZetValue (Dim, Value, TemplateSheetId) VALUES (@dim, @value, @templateSheetId)";
                    connectionInsurance.Execute(sqlZet, new { dim = zetParts[0], value = zetParts[1], templateSheetId = sheetId });
                    Console.Write(',');
                }
            }

            return sheet;
        }



        private MAPPING FindMappingRowColForTheCell(MTable table, MTableCell cell)
        {
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            // given the Cell,  Find the rowcol using the MAPPINGS table (plus the y,z on the mTable)
            // A list of MAPPINGS with the same row/col will correspond to the the noOpenDPS siganture of the cell            
            // -- return only the first mapping (it is enough to give you the row/col)

            //var isOpenTable = table.IsOpenTable;
            //var cellSignatureFull = isOpenTable ? cell.DPS : cell.NoOpenDPS;


            var cellSignatureFull = cell.DPS;
            var cellSignature = Regex.Replace(cellSignatureFull, @"\(\*(.*?)\)", "(*)");  ////s2c_dim:LG(*[GA_18;x0;0])=>s2c_dim:LG(*)


            var selectRowSql = @"
                SELECT
                  ms.Signature
                 ,ms.RowCol
                FROM dbo.MappingSignatures ms
                WHERE ms.InstanceId = @DocumentId
                AND ms.TableId = @TableId
                AND ms.Signature = @Signature
                ";
            var mappingSig = connectionInsurance.QuerySingleOrDefault<MappingSignatures>(selectRowSql, new { DocumentId, table.TableID, signature = cellSignature });

            if (mappingSig is null)
            {
                return new MAPPING();
            }

            var sqlMapping = @"
                SELECT map.DYN_TAB_COLUMN_NAME, map.DIM_CODE, map.DOM_CODE, map.ORIGIN, map.DATA_TYPE, map.IS_PAGE_COLUMN_KEY, map.IS_IN_TABLE
                FROM MAPPING map
                WHERE 
                    map.TABLE_VERSION_ID =@tableId	                        
                    AND DYN_TAB_COLUMN_NAME = @rowCol;    
                ";
            var mapping = connectionEiopa.QueryFirstOrDefault<MAPPING>(sqlMapping, new { tableId = cell.TableID, rowCol = mappingSig.RowCol }) ?? new MAPPING();

            return mapping;
        }




        private List<MTable> GetFiledModuleTables()
        {

            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var sqlModule = @"select mod.ModuleID from mModule mod where mod.ModuleCode = @moduleCode";
            ModuleId = connectionEiopa.QuerySingleOrDefault<int>(sqlModule, new { ModuleCode });

            var sqlTables = @"
              SELECT 	  
                    tab.TableID,
                    tab.TableCode,                    
                    tab.XbrlFilingIndicatorCode,
                    tab.XbrlTableCode,
                    tab.YDimVal,
                    tab.ZDimVal,
                    tab.TableLabel
                  FROM mModuleBusinessTemplate mbt
                  left outer join mTemplateOrTable  va on va.TemplateOrTableID =mbt.BusinessTemplateID
                  left outer join mTemplateOrTable bu on bu.ParentTemplateOrTableID = va.TemplateOrTableID
                  left outer join mTemplateOrTable anno on anno.ParentTemplateOrTableID = bu.TemplateOrTableID
                  left outer join mTaxonomyTable taxo on taxo.AnnotatedTableID=anno.TemplateOrTableID
                  left outer join mTable tab on tab.TableID = taxo.TableID
                  where mbt.ModuleID = @moduleId";
            var moduleTables = connectionEiopa.Query<MTable>(sqlTables, new { ModuleId }).ToList();

            
            var validModuleTables = moduleTables.Where(mtable => Filings.Any(filing => mtable.TableCode.Contains(filing))).ToList();


            return validModuleTables;
        }


        private  static (string xbrl, string signature) CleanCellSignature(string cellSignature)
        {
            //var test= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)|s2c_dim:DI(s2c_DI:x5)|s2c_dim:OC(*?[237])|s2c_dim:RB(*[332;1512;0])|s2c_dim:RM(s2c_TI:x44)|s2c_dim:TB(s2c_LB:x28)|s2c_dim:VG(s2c_AM:x80)";
            //var test2 = @"MET(s2md_met:mi1104)|s2c_dim:BL(*[334;1512;0])|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(*)|s2c_dim:RD(*)|s2c_dim:RE(*)";
            
            //cellSignature = test2;
            var dimList = cellSignature.Split("|")
            .Where(dim => !dim.Contains("?"))
            .Select(dim => Regex.Replace(dim, @"\[.*\]", ""))
            .Select(dim => dim.Replace("*", "%")).ToList();
            var cleanSig = string.Join("|", dimList);

            var xbrlMetric = dimList.First();
            var xbrlCode = GeneralUtils.GetRegexSingleMatch(@"MET\((.*?)\)", xbrlMetric);
            return (xbrlCode, cleanSig);
        }

        public static List<TemplateSheetFact> FindMatchingFactsRegex(ConfigObject config, int documentId, string cellSignature)
        {            
            //MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)|s2c_dim:DI(s2c_DI:x5)|s2c_dim:OC(*?[237])|s2c_dim:RB(*[332;1512;0])|s2c_dim:RM(s2c_TI:x44)|s2c_dim:TB(s2c_LB:x28)|s2c_dim:VG(s2c_AM:x80)
            //find the list of facts that match the dimensions of the cell
            //the cells may have open dimensions * but the facts have the real dimdom values from the context
            //More than one fact may be found because of open Z, or even open fact dim (for currency or country for example)
            //** the fact signature has the REAL value (From its context) 
            //** A cell signature may have optional dims : s2c_dim:FN(*?[16]) 
            //-- the cells' signature with wildcards: s2c_dim:FN(*)

            //Cell signature MET(s2md_met:mi686)|s2c_dim:AO(*?[16])|s2c_dim:EA(s2c_VM:x23)|s2c_dim:RT(s2c_RT:x97)|s2c_dim:VG(s2c_AM:x80)

            //both fact signatures both are valid
            //MET(s2md_met:mi686)|s2c_dim:AO(s2c_LB:x93)|s2c_dim:EA(s2c_VM:x23)|s2c_dim:RT(s2c_RT:x97)|s2c_dim:VG(s2c_AM:x80)
            //MET(s2md_met:mi686)|s2c_dim:EA(s2c_VM:x23)|s2c_dim:RT(s2c_RT:x97)|s2c_dim:VG(s2c_AM:x80)
            //this is invalid
            //MET(s2md_met:mi686)|s2c_dim:EA(s2c_VM:x23)|s2c_dim:RT(s2c_RT:x97)|s2c_dim:VG(s2c_AM:x80)|s2c_dim:BB(s2c_AM:x80)
            //MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(*?[237])|s2c_dim:RM(s2c_TI:x49)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)

            using var connectionInsurance = new SqlConnection(config.LocalDatabaseConnectionString);
            (var xbrlCode2, var cleanSignature2) = CleanCellSignature(cellSignature);
            var dimList = cleanSignature2.Split("|").ToList();
                        
            if (dimList.Count < 1)
            {
                return new List<TemplateSheetFact>();
            }

            var xbrlMetric = dimList.First();
            var xbrlCode = GeneralUtils.GetRegexSingleMatch(@"MET\((.*?)\)", xbrlMetric);


            var sqlSelectFacts = @"
              SELECT  
                  fact.FactId
                 ,fact.TemplateSheetId
                 ,fact.Row
                 ,fact.Col
                 ,fact.Zet
                 ,fact.CellID
                 ,fact.FieldOrigin
                 ,fact.TableID
                 ,fact.DataPointSignature
                 ,fact.Unit
                 ,fact.Decimals
                 ,fact.NumericValue
                 ,fact.BooleanValue
                 ,fact.DateTimeValue
                 ,fact.TextValue
                 ,fact.DPS
                 ,fact.IsRowKey
                 ,fact.IsShaded
                 ,fact.XBRLCode
                 ,fact.DataType
                 ,fact.DataPointSignatureFilled                 
                 ,fact.InternalRow
                 ,fact.internalCol
                 ,fact.DataTypeUse
                 ,fact.IsEmpty
                 ,fact.IsConversionError
                 ,fact.ZetValues
                 ,fact.OpenRowSignature
                 ,fact.CurrencyDim                 
                 ,fact.contextId                 
                 ,fact.Signature
                 ,fact.RowSignature                 
                 ,fact.InstanceId                  

                FROM dbo.TemplateSheetFact fact
                WHERE fact.InstanceId = @documentId
                AND fact.XBRLCode = @xbrlCode
                AND fact.DataPointSignatureFilled like @sig;
             ";
            var factList = connectionInsurance.Query<TemplateSheetFact>(sqlSelectFacts, new { documentId, xbrlCode,sig=cleanSignature2 }).ToList();
            if (factList.Count == 0)
            {
                return factList;
            }

            //This is an extra filtering of facts when there is a cell which specified a dims hierarchy 
            if (cellSignature.Contains("*["))
            {
                factList = factList.Where(fact => IsFactSignatureMatchingExpensive(config, cellSignature, fact.DataPointSignatureFilled)).ToList();
            }

            //some facts may exist in many tables (we only need one)
            var distinctList = new List<TemplateSheetFact>();
            foreach(var fact in factList)
            {
                var found = distinctList.Exists(dfact => dfact.DataPointSignatureFilled == fact.DataPointSignatureFilled);
                if (!found)
                {
                    distinctList.Add(fact);
                }
            }

            return distinctList;
            
        }



        public static bool IsFactSignatureMatchingExpensive(ConfigObject config, string cellSignature, string factSignature)
        {
            //check all fact dims against cell dims the expenive way
            //the check looks for valid hierarchy members[323;3;3] 
            var factDims = factSignature.Split("|").ToList();
            var cellDimsFull = cellSignature.Split("|");
            var cellDims = cellDimsFull.Where(dim => !dim.Contains("?[")); // the dims with "?" are default and do NOT appear in the xbrl context

            foreach (var cellDim in cellDims)
            {

                var factDimFound = factDims.FirstOrDefault(factDim => IsFactDimMatchingCellExpensive(config, cellDim, factDim));
                if (factDimFound is null)
                {
                    return false;
                }
                factDims.Remove(factDimFound);

            }
            return factDims.Count == 0;

        }


        private static bool IsFactDimMatchingCellExpensive(ConfigObject config, string cellDim, string factDim)
        {            
            //            
            //*  "*" allows for any value but brackets constrain the values to the hierechy members
            ////MET(s2md_met:mi686)|s2c_dim:AO(*?[16])|s2c_dim:EA(s2c_VM:x23)|s2c_dim:RT(s2c_RT:x97)|s2c_dim:VG(s2c_AM:x80)
            //MET(s2md_met:mi1157)|s2c_dim:BL(*[334;1512;0])|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(*)|s2c_dim:RD(*)|s2c_dim:RE(*)
            //MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(*?[237])|s2c_dim:RM(s2c_TI:x49)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)

            var isExact = !cellDim.Contains("*");
            if (isExact)
            {
                return factDim == cellDim;

            }
            else
            {
                var factDimDom = DimDom.GetParts(factDim);
                var cellDimDom = DimDom.GetParts(cellDim);

                if (factDimDom.Dim != cellDimDom.Dim)
                {
                    return false;
                }

                //***  Dim is the same , so check if  open
                if (cellDimDom.DomAndValRaw == "*")
                {
                    return true;
                }

                //check if the fact's dom value belongs in the hierarchy
                using var connectionEiopa = new SqlConnection(config.EiopaDatabaseConnectionString);

                var hierarchyParts = GeneralUtils.GetRegexSingleMatch(@"\[(.*)\]", cellDimDom.DomAndValRaw).Split(";");
                if (hierarchyParts.Length < 1)
                {
                    return false;
                }

                var hierarchyId = hierarchyParts[0];
                var sqlSelectMem = @"select MemberID from mMember mem where mem.MemberXBRLCode=@MemberXBRLCode";
                var memberId = connectionEiopa.QueryFirstOrDefault<int>(sqlSelectMem, new { MemberXBRLCode=factDimDom.DomAndValRaw });
                if(memberId ==0)
                {
                    return false;
                }

                var sqlSelectHiMembers = @"select nod.HierarchyID from mHierarchyNode nod where nod.HierarchyID= @HierarchyID and nod.MemberID = @MemberID";
                var hierarchyNode = connectionEiopa.QueryFirstOrDefault<int>(sqlSelectHiMembers, new { hierarchyId, memberId });
                return hierarchyNode > 0;

            }


        }

        static string GetWildDims(string factSignature, string zDimVal)
        {
            //find the dims of a fact which correspond to the wild zet dims of the table
            //the factOpenZetdims  will be used to group facts with the same tablecode but in different sheets
            //multiple sheets with the same table code

            //table 124
            //table ZDimVal : MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:OC(*?[237])|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)
            //DataPointSignatureFilled:  MET(s2md_met:mi289)|s2c_dim:AX(s2c_AM:x4)|s2c_dim:BL(s2c_LB:x34)|s2c_dim:DY(s2c_TI:x1)|s2c_dim:RM(s2c_TI:x49)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)
            // result : "s2c_dim:AX(s2c_AM:x4)|s2c_dim:BL(s2c_LB:x34)"

            var factAllDims = factSignature?.Split("|")?.ToList() ?? new List<string>();
            //--only use the table wild zet dims. Non-wild zet dims will be the same for all facts in the same table 
            //select only the Dim part (AX, BL, DY, ...)
            var tableZetOpenDims = zDimVal
            ?.Split("|")
            ?.Where(dim => dim.Contains("*"))
            ?.Select(dim => DimDom.GetParts(dim).Dim)?.ToList() ?? new List<string>();

            var factZetDims = factAllDims?.Where(dim => tableZetOpenDims.Exists(tblDim => dim.Contains(tblDim))).ToList() ?? new List<string>();

            var zetStr = string.Join("|", factZetDims);

            return zetStr;

        }


        private static  string GetTablePivotZet(ConfigObject confg, MTable table)
        {
            //find the in table zet dim which is not in the table zet dims but exists in the mappings as Is_in_table=1
            //for example in table 40, we have multiple facts in the same cell
            //the differenciating dim is not present in table zet dims. (which means is in the same sheet)
            //Assume we can only have one differenciating dim
            using var connectionEiopa = new SqlConnection(confg.EiopaDatabaseConnectionString);
            var sqlInsideDims = @"
                select map.DIM_CODE from MAPPING map where 
                map.ORIGIN='C'
                and map.IS_IN_TABLE=1
                and map.IS_PAGE_COLUMN_KEY=1
                and map.TABLE_VERSION_ID = @tableId
                ";
            var inTableDims = connectionEiopa.Query<string>(sqlInsideDims, new { table.TableID })?.ToList();
            if (inTableDims.Count == 0)
            {
                return "";
            }
            
            var zdims = table.ZDimVal?.Split("|")
                ?.Select(dim => DimDom.GetParts(dim).Dim)
                ?.ToList() ?? new List<string>();

            var filterDims = inTableDims
                ?.Where(inTabDim => !zdims.Contains(DimDom.GetParts(inTabDim).Dim))
                ?.ToList() ?? new List<string>();
            if (inTableDims.Count != filterDims.Count)
            {
                var message = $"filterDimd different table:{table.TableCode}";
                Console.WriteLine(message);
                Log.Error(message);
            }

            if (filterDims.Count > 0)
            {
                return filterDims.First();
            }
            return "";

        }

        private int CreateYFactsInDb(int sheetId)
        {
            //open tables: need to create y cells in EVERY row because they are NOT written as facts in xbrl files, but they are lines in the context 
            //for every row we need to create one cell for EACH Y dim column.(* may be more than one Y dim)            
            //RowContexts were created when preparing the facts



            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);


            var sqlSelectSheet = @"select TemplateSheetId, TableCode, SheetCode, TableID from TemplateSheetInstance where TemplateSheetId =@sheetId";
            var sheet = connectionInsurance.QuerySingleOrDefault<TemplateSheetInstance>(sqlSelectSheet, new { sheetId });
            if (sheet is null)
            {
                return 0;
            }

            //DISTINCT InternalRow AND OpenRowSignature because need only one fact per row with the unique OpenRowSignature
            var sqlFactSig = @"select distinct fact.InternalRow, OpenRowSignature,zet,zetValues from TemplateSheetFact fact  where TemplateSheetId= @sheetId";
            var factsWithRowSignature = connectionInsurance.Query<TemplateSheetFact>(sqlFactSig, new { sheetId }) ?? new List<TemplateSheetFact>();

            foreach (var factWithRowSignature in factsWithRowSignature)
            {

                var factYdims = factWithRowSignature?.OpenRowSignature?.Split("|",StringSplitOptions.RemoveEmptyEntries).ToList();
                if (factYdims is null)
                {
                    return 0;
                }

                //***********************
                //var sqlTable = @"s2c_dim:BL(*[350;1512;0])|s2c_dim:LP(*)|s2c_dim:OD(*)|s2c_dim:RE(*)|s2c_dim:ST(*)";                
                var sqlTable = @"select YDimVal from  mTable tab where tab.TableID= @tableId";
                var yTableDimStr = connectionEiopa.QuerySingleOrDefault<string>(sqlTable, new { sheet.TableID });
                if(yTableDimStr is null)
                {
                    return 0;
                }
                var tableYdims = yTableDimStr.Split("|",StringSplitOptions.RemoveEmptyEntries);
                
                foreach(var tableYdim in tableYdims)
                {
                    var yTableDimDom = DimDom.GetParts(tableYdim);
                    var sqlFindMapping = @"
                            SELECT
                              map.DYN_TAB_COLUMN_NAME
                             ,map.DIM_CODE
                             ,map.DOM_CODE
                             ,map.ORIGIN
                             ,map.DATA_TYPE
                             ,map.IS_PAGE_COLUMN_KEY
                             ,map.IS_IN_TABLE
                            FROM MAPPING map
                            WHERE 
				            map.DYN_TAB_COLUMN_NAME not like 'PAGE%'
				            and map.ORIGIN = 'C'
                            AND map.IS_IN_TABLE = 1
                            AND map.TABLE_VERSION_ID = @tableId
				            and map.DIM_CODE like @dimCode
				
                    ";
                    var yDimMapping = connectionEiopa.QueryFirstOrDefault<MAPPING>(sqlFindMapping, new { tableId = sheet.TableID, dimCode = $"s2c_dim:{yTableDimDom.Dim }%" });
                    if(yDimMapping is null)
                    {
                        continue;
                    }
                    
                    var factYdim = factYdims.FirstOrDefault(dim => DimDom.GetParts(dim).Dim== yTableDimDom.Dim);
                    if(factYdim is null)
                    {
                        continue;
                    }
                    
                    var factDimDomValue = DimDom.GetParts(factYdim);
                    var signatureFilled = $"YR|{DimDom.GetParts( factYdim).Dim}|{factWithRowSignature.OpenRowSignature}";
                    var newFact = new TemplateSheetFact()
                    {
                        InstanceId = DocumentId,
                        TemplateSheetId = sheet.TemplateSheetId,
                        TableID = sheet.TableID,
                        Unit = "",                        
                        DataType="",
                        DataTypeUse = tableYdim.Contains("[") ? "E" : "S",
                        XBRLCode = "RowKey",
                        DataPointSignatureFilled = signatureFilled,
                        OpenRowSignature = factWithRowSignature.OpenRowSignature,
                        CellID = 0,// cell does not exist for column cells
                        IsRowKey = true, //rowMapping.IS_PAGE_COLUMN_KEY,
                        Row = $"R{factWithRowSignature.InternalRow:D4}", // rowContext.RowNumber,
                        Col = yDimMapping.DYN_TAB_COLUMN_NAME,
                        Zet = factWithRowSignature.Zet,
                        InternalRow = factWithRowSignature.InternalRow,
                        InternalCol = 0,
                        TextValue = tableYdim.Contains("[") ? factDimDomValue.DomAndValRaw :  factDimDomValue.DomValue,
                        FieldOrigin = "KYR",
                        CurrencyDim = "VV",
                        ZetValues = factWithRowSignature.ZetValues


                    };
                    newFact.ConvertTextValue();

                    var SqlInsertTemplateSheetFact = @"
                        INSERT INTO [dbo].[TemplateSheetFact]
                           (
                            [InstanceId]
                           ,[TemplateSheetId]           
                           ,[TableID]
                           ,[Unit]
                           ,[DataType]
                           ,[DataTypeUse]
                           ,[XbrlCode]
                           ,[DataPointSignature]
                           ,[DataPointSignatureFilled]
                           ,[OpenRowSignature]
                           ,[CellID]
                           ,[IsRowKey]
                           ,[Row]
                           ,[Col]
                            ,Zet
                           ,[InternalRow]
                           ,[InternalCol]           
                           ,[TextValue] 
                           ,[NumericValue]
                           ,[DateTimeValue]
                           ,[BooleanValue]    
                           ,[IsConversionError]
                           ,[CurrencyDim]
                           ,ZetValues
                           ,[FieldOrigin]
                            )
                        VALUES
                           (            
                            @InstanceId
                           ,@TemplateSheetId           
                           ,@TableID                           
                           ,@unit
                           ,@DataType
                           ,@DataTypeUse
                           ,@XbrlCode
                           ,@DataPointSignature
                           ,@DataPointSignatureFilled
                           ,@OpenRowSignature
                           ,@CellID
                           ,@IsRowKey
                           ,@Row
                           ,@Col           
                           ,@Zet
                           ,@InternalRow
                           ,@InternalCol           
                           ,@TextValue        
                           ,@NumericValue
                           ,@DateTimeValue
                           ,@BooleanValue      
                           ,@IsConversionError
                           ,@CurrencyDim
                           ,@ZetValues
                           ,@FieldOrigin
                           )
                        ";

                    var res = connectionInsurance.Execute(SqlInsertTemplateSheetFact, newFact);
                    Console.Write(".");

                }

            }
            return 1;
        }


        private void UpdateSheetTabNames(string tableCode)
        {
            //excel tab sheet names cannot exceed 30 chars
            //sheetCodes exceet the limit because we add zetDims to the tablecode (create one sheet for each z dim)
            //therefore, build sheetTabName which is the first 25 chars of the sheetcode + serial (resets for every tableCode)
            //if there is no open Z do not add the z in name
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            var count = 0;
            var sqlSelSheets = @"select TemplateSheetId, SheetCode, TableCode from  TemplateSheetInstance where InstanceId= @documentId and TableCode = @tableCode";
            var sheets = connectionInsurance.Query<TemplateSheetInstance>(sqlSelSheets, new { DocumentId, tableCode }) ?? new List<TemplateSheetInstance>(); ;
            foreach (var sheet in sheets)
            {

                var sqlTableCode = @"select ZDimVal from mTable where mTable.TableCode = @tableCode";
                var table = connectionEiopa.QueryFirstOrDefault<MTable>(sqlTableCode, new { tableCode });

                var isOpenZet = table.ZDimVal?.Contains("*") ?? false;

                var sheetTabName = isOpenZet
                    ? $"{tableCode.Trim()}#{count++:D2} "
                    : tableCode.Trim(); //if no open z then just use the tablecode as the sheettab name

                var sqlUpdSheet = @"update TemplateSheetInstance set SheetTabName= @SheetTabName where TemplateSheetId = @TemplateSheetId";
                connectionInsurance.Execute(sqlUpdSheet, new { SheetTabName = sheetTabName, sheet.TemplateSheetId });
            }


        }


        private static List<string> ConstructFactFullZetList(string factSignature, string tabeZetSignature)
        {
            //find the dims of the fact that are contained in the table zet. table zet Dims can be explcit or wild (open)
            //normally zetSignatrue does not contain a Met but there is one case. Do not store it as dimension
            //zetSignature = @"MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:OC(*?[237])|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";
            //factSignature = @"MET(s2md_met:mi289)|s2c_dim:AF(s2c_CA:x1)|s2c_dim:AX(s2c_AM:x4)|s2c_dim:BL(s2c_LB:x73)|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(s2c_CU:EUR)|s2c_dim:RM(s2c_TI:x49)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";

            var tabeZetList = tabeZetSignature?.Split("|")?.ToList() ?? new List<string>();
            if (tabeZetList.Count == 0)
            {
                return tabeZetList;
            }

            var zetOpenList = tabeZetList.Where(dim => dim.Contains("*")).ToList();
            var zetClosedList = tabeZetList.Where(dim => !dim.Contains("*")).ToList(); ;
            var factDims = factSignature?.Split("|")?.ToList() ?? new List<string>();

            var zetFinalList = new List<string>();

            foreach (var zetDim in zetOpenList)
            {

                var zetDimPart = GeneralUtils.GetRegexSingleMatch(@"(s2c_dim.*?:\w\w)", zetDim);//s2c_dim:AF(*?[59]) => s2c_dim:AF
                var factDim = factDims.SingleOrDefault(dim => dim.Contains(zetDimPart));

                if (factDim is not null)
                {
                    var fff = DimDom.GetParts(factDim);
                    var factDimPart = GeneralUtils.GetRegexSingleMatch(@"s2c_dim:(\w\w)", factDim);//"s2c_dim:AF(s2c_CA:x1)=> AF
                    var factDomPart = GeneralUtils.GetRegexSingleMatch(@"s2c_dim:\w\w\((.*?)\)", factDim); //"s2c_dim:AF(s2c_CA:x1)=> s2c_CA:x1                                        
                    zetFinalList.Add($"{factDimPart}#{factDomPart}");
                }
            }

            foreach (var dim in zetClosedList)
            {
                var zetDimPart = GeneralUtils.GetRegexSingleMatch(@"s2c_dim:(\w\w)", dim); //"s2c_dim:TA(s2c_AM:x57)=>TA
                var zetDomPart = GeneralUtils.GetRegexSingleMatch(@"s2c_dim:\w\w\((s2c_.*?)\)", dim);// "s2c_dim:TA(s2c_AM:x57)=> AM:x57
                var xxx = DimDom.GetParts(dim);
                if (!string.IsNullOrEmpty(zetDimPart) && !string.IsNullOrEmpty(zetDomPart))
                {
                    zetFinalList.Add($"{zetDimPart}#{zetDomPart}");
                }

            }

            zetFinalList.Sort();
            return zetFinalList;

        }


        private void DeleteMappingSignature(int instanceId)
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqlDeleteSig = @"delete  FROM MappingSignatures  WHERE InstanceId = @InstanceId";
            connectionInsurance.Execute(sqlDeleteSig, new { instanceId });

        }


    }
}
