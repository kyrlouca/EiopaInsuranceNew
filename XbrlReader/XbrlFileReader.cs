using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using HelperInsuranceFunctions;
using EntityClassesZ;
using Serilog;
using GeneralUtilsNs;
using EiopaConstants;
using ConfigurationNs;
using TransactionLoggerNs;
using EntityClasses;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Text.RegularExpressions;

namespace XbrlReader
{
    public class XbrlFileReader
    {
        //Reads an xbr
        //An XBRL file corresponds to a MODULE (qrg, afg, etc)
        //The XbrlReader creates structures for the data contained in an XBRL file (units, facts, contexts, fileinfo)        
        //It does NOT  assign row/col to the facts and it does NOT save to the database
        //It is the DataProcessor which does the processing and saving in db


        public bool IsValidEiopaVersion { get; private set; }
        public bool IsValidProcess { get; private set; } = true;
        public int CurrencyBatchId { get; private set; }
        public string DefaultCurrency { get; set; } = "EUR";
        public DateTime StartTime { get; } = DateTime.Now;

        public ConfigObject ConfigObject { get; private set; }

        public XDocument XmlDoc { get; private set; }
        public int UserId { get; private set; }
        public int FundId { get; private set; }
        public int ApplicableYear { get; private set; }
        public int ApplicableQuarter { get; private set; }
        public string FileName { get; private set; }


        public string ModuleCode { get; private set; }

        public MModule Module { get; private set; }
        public int DocumentId { get; internal set; }

        public List<string> FilingsSubmitted { get; set; } = new();
        public Dictionary<string, string> Units { get; protected set; } = new Dictionary<string, string>();

        public string SolvencyVersion { get; internal set; }

        public XElement RootNode { get; private set; }
        readonly XNamespace xbrli = "http://www.xbrl.org/2003/instance";
        readonly XNamespace xbrldi = "http://xbrl.org/2006/xbrldi";
        readonly XNamespace xlink = "http://www.w3.org/1999/xlink";
        readonly XNamespace link = "http://www.xbrl.org/2003/linkbase";
        //readonly XNamespace typedDimNs = "http://eiopa.europa.eu/xbrl/s2c/dict/typ";
        readonly XNamespace findNs = "http://www.eurofiling.info/xbrl/ext/filing-indicators";

        public static void ProcessXbrlFile (string solvencyVersion, int currencyBatchId, int userId, int fundId, string moduleCode, int applicableYear, int applicableQuarter, string fileName)
        {
            var reader = new XbrlFileReader(solvencyVersion, currencyBatchId, userId, fundId, moduleCode, applicableYear, applicableQuarter, fileName);

            var existingDocs = reader.GetExistingDocuments();

            var isLockedDocument = existingDocs.Any(doc => doc.Status.Trim() == "P" || doc.Status.Trim() == "S");
            if (isLockedDocument){
                var existingDoc = existingDocs.First();
                var existingDocId = existingDoc.InstanceId;
                var status = existingDoc.Status.Trim();

                var message = $"Cannot create Document with Id: {existingDoc.InstanceId}. The document is already validated with status :{existingDoc.Status}";
                if (status == "P")
                {
                    message = $"Cannot create Document with Id: {existingDoc.InstanceId}. The Document is already being processed with status :{existingDoc.Status}";
                }
                Log.Error(message);
                Console.WriteLine(message);

                var trans = new TransactionLog()
                {
                    PensionFundId = reader.FundId,
                    ModuleCode = reader.ModuleCode,
                    ApplicableYear = reader.ApplicableYear,
                    ApplicableQuarter = reader.ApplicableQuarter,
                    Message = message,
                    UserId = reader.UserId,
                    ProgramCode = ProgramCode.RX.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = existingDoc.InstanceId,
                    MessageType = MessageType.ERROR.ToString()
                };
                TransactionLogger.LogTransaction(reader.SolvencyVersion, trans);
                reader.IsValidProcess = false;
                return;
            }


            //delete older versions (except from locked or submitted)
            existingDocs.Where(doc => doc.Status.Trim() != "P" && doc.Status.Trim() != "S")
                .ToList()
                .ForEach(doc => reader.DeleteDocument(doc.InstanceId));
           
            reader.WriteProcessStarted();

            //*** create the document anyway so we can attach log errors
            var documentId = reader.CreateDocInstanceInDb();

            //***Create loose facts not assigned to sheets
            var isValid = reader.CreateLooseFacts(documentId, fileName);
            if (!isValid)
            {
                var message = $"Document not created";
                Log.Error(message);
                Console.WriteLine(message);
                //Update status
                reader.UpdateDocumentStatus("E");
                reader.IsValidProcess = false;
                return;
            }


             FactsProcessor.ProcessFactsAndAssignToSheets(reader.SolvencyVersion, reader.DocumentId, reader.FilingsSubmitted);
            
            var diffminutes = DateTime.Now.Subtract(reader.StartTime).TotalMinutes;
            Log.Information($"XbrlFileReader Minutes:{diffminutes}");


        }


        private XbrlFileReader()
        {
            Console.WriteLine("ONLY for testing XbrlData");
            return;
        }

        private XbrlFileReader(string solvencyVersion, int currencyBatchId, int userId, int fundId, string moduleCode, int applicableYear, int applicableQuarter, string fileName)
        {
            //Read an Xbrl file and store the data in structures (dictionary of units, contexs, facts)
            //Then store document, sheets, and facts in database
            SolvencyVersion = solvencyVersion;
            CurrencyBatchId = currencyBatchId;
            UserId = userId;
            FundId = fundId;
            ModuleCode = moduleCode;
            ApplicableYear = applicableYear;
            ApplicableQuarter = applicableQuarter;
            FileName = fileName;


            ConfigObject = GetConfiguration();
            if (ConfigObject is null)
            {
                return;
            }

            IsValidEiopaVersion = Configuration.IsValidVersion(SolvencyVersion);

            Module = GetModule(ModuleCode);
            if (Module is null)
            {
                var message = $"Invalid Module code {ModuleCode}";
                Log.Error(message);
                Console.WriteLine(message);
                var trans = new TransactionLog()
                {
                    PensionFundId = FundId,
                    ModuleCode = ModuleCode,
                    ApplicableYear = ApplicableYear,
                    ApplicableQuarter = ApplicableQuarter,
                    Message = message,
                    UserId = UserId,
                    ProgramCode = ProgramCode.RX.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = 0,
                    MessageType = MessageType.INFO.ToString(),                    
                };
                TransactionLogger.LogTransaction(SolvencyVersion, trans);

                return;
            }
            ///

        }

        private ConfigObject GetConfiguration()
        {

            if (!Configuration.IsValidVersion(SolvencyVersion))
            {
                var errorMessage = $"XbrlFileReader --Invalid Eiopa Version: {SolvencyVersion}";
                Console.WriteLine(errorMessage);
                Log.Error(errorMessage);
                return null ;
            }

            var configObject = Configuration.GetInstance(SolvencyVersion).Data;
            if (string.IsNullOrEmpty(configObject.LoggerXbrlFile))
            {
                var errorMessage = "LoggerXbrlFile is not defined in ConfigData.json";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(configObject.LoggerXbrlReaderFile, rollOnFileSizeLimit: true, shared: true, rollingInterval: RollingInterval.Day)
            .CreateLogger();


            if (string.IsNullOrEmpty(configObject.EiopaDatabaseConnectionString) || string.IsNullOrEmpty(configObject.LocalDatabaseConnectionString))
            {
                var errorMessage = "Empty ConnectionStrings";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }


            return configObject;
        }

        private bool CreateLooseFacts(int documentId, string sourceFile)
        {
            //Parse an xbrl file and create on object of the class which has the contexts, facts, etc
            //However, with the new design design, contexts and facts are saved in memory tables and NOT in data structures            

            DocumentId = documentId;

            if (!File.Exists(sourceFile))
            {
                var message = $"XBRL CreateXbrlDocument ERROR: Document not Found : {sourceFile}";
                Console.WriteLine($"**** {message}");
                Log.Error(message);

                var trans = new TransactionLog()
                {
                    PensionFundId = FundId,
                    ModuleCode = ModuleCode,
                    ApplicableYear = ApplicableYear,
                    ApplicableQuarter = ApplicableQuarter,
                    Message = message,
                    UserId = UserId,
                    ProgramCode = ProgramCode.RX.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = documentId,
                    MessageType = MessageType.ERROR.ToString(),
                    FileName= sourceFile
                };
                TransactionLogger.LogTransaction(SolvencyVersion, trans);

                return false;
            }
            


            XDocument xmlDoc;
            try
            {
                xmlDoc = XDocument.Load(sourceFile);
            }
            catch (Exception e)
            {
                var message = $" ERROR Cannot parse XBRL file : {sourceFile}";
                Log.Error(message);
                Log.Error(e.Message);
                Console.WriteLine(e);

                var trans = new TransactionLog()
                {
                    PensionFundId = FundId,
                    ModuleCode =ModuleCode,
                    ApplicableYear = ApplicableYear,
                    ApplicableQuarter = ApplicableQuarter,
                    Message = message,
                    UserId = UserId,
                    ProgramCode = ProgramCode.RX.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = documentId,
                    MessageType = MessageType.ERROR.ToString(),
                    FileName = sourceFile
                };

                TransactionLogger.LogTransaction(SolvencyVersion, trans);
                return false;
            }

            RootNode = xmlDoc.Root;

            var reference = RootNode.Element(link + "schemaRef").Attribute(xlink + "href").Value;
            var moduleCodeXbrl = GeneralUtils.GetRegexSingleMatch(@"http.*mod\/(\w*)", reference);
            if (moduleCodeXbrl != Module.ModuleCode)
            {
                var message = @$" Module Code provided by Fund: ""{Module.ModuleCode}"" is DIFFERENT THAN  Module Code in Xbrl File : ""{moduleCodeXbrl}""";
                Log.Error(message);
                Console.WriteLine(message);

                var trans = new TransactionLog()
                {
                    PensionFundId = FundId,
                    ModuleCode = moduleCodeXbrl,
                    ApplicableYear = ApplicableYear,
                    ApplicableQuarter = ApplicableQuarter,
                    Message = message,
                    UserId = UserId,
                    ProgramCode = ProgramCode.RX.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = documentId,
                    MessageType = MessageType.ERROR.ToString(),
                    FileName = sourceFile
                };
     
                TransactionLogger.LogTransaction(SolvencyVersion, trans);
                return false;
            }

            Console.WriteLine($"Opened Xblrl=>  Module: {moduleCodeXbrl} ");            
            
            //DocumentId = CreateDocInstanceInDb();
            
            AddValidFilingIndicators();
            Console.WriteLine("filing Indicators");

            Console.WriteLine("\nCreate Units");
            AddUnits();

            Console.WriteLine("\nCreate Contexts");
            AddContexts();

            Console.WriteLine("\nCreate Facts");
            AddFacts();

            DeleteContexts();
            return  true;

        }

        private void DeleteContexts()
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            connectionInsurance.Execute("Delete from Context where InstanceId= @DocumentId", new { DocumentId });
        }

        private void AddValidFilingIndicators()
        {
            //filing indicators
            var filingsHeader = RootNode.Element(findNs + "fIndicators");
            var filingIndicators = filingsHeader?.Elements(findNs + "filingIndicator").ToList();
            foreach (var fi in filingIndicators)
            {
                var isNotFiled = fi.Attribute(findNs + "filed")?.Value == "false";
                if (isNotFiled)
                {
                    continue;
                }
                FilingsSubmitted.Add(fi.Value);
            }
        }

        private int CreateDocInstanceInDb()
        {
            using var connection = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            var sqlInsertDoc = @"
               INSERT INTO DocInstance
                   (                                            
                    [PensionFundId]                   
                   ,[UserId]                   
                   ,[ModuleCode]           
                   ,[ApplicableYear]
                   ,[ApplicableQuarter]                   
                   ,[ModuleId]      
                   ,[FileName]
                   ,[CurrencyBatchId]
                   ,[Status]
                   ,[EiopaVersion]
                    )
                VALUES
                   (                                
                    @PensionFundId
                   ,@UserId
                   ,@ModuleCode                   
                   ,@ApplicableYear
                   ,@ApplicableQuarter                   
                   ,@ModuleId
                   ,@FileName
                   ,@CurrencyBatchId
                   ,@Status
                   ,@EiopaVersion
                    ); 
                SELECT CAST(SCOPE_IDENTITY() as int);
                ";



            var sqlModule = @"select mod.ModuleID from mModule mod where mod.ModuleCode = @moduleCode";
            var moduleId = connectionEiopa.QuerySingleOrDefault<int>(sqlModule, new { ModuleCode });

            var doc = new
            {
                PensionFundId = FundId,
                UserId,
                ModuleCode,
                ApplicableYear,
                ApplicableQuarter,
                ModuleId = moduleId,
                FileName,
                CurrencyBatchId,
                Status = "P",
                EiopaVersion=SolvencyVersion,
            };


            var result = connection.QuerySingleOrDefault<int>(sqlInsertDoc, doc);
            return result;
        }

        private void AddUnits()
        {
            //units
            var units = RootNode.Elements(xbrli + "unit");
            foreach (var unit in units)
            {
                var id = unit.Attribute("id").Value;
                var measure = unit.Element(xbrli + "measure").Value;
                Units.Add(id, measure);
            }
        }

        private void AddContexts()
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);




            //ADD all the context elements 
            //Each contextElement has contextLines (typed and implicit)
            var contextElements = RootNode.Elements(xbrli + "context");
            var i = 0;
            foreach (var contextElement in contextElements)
            {
                //read the explicit and typed dimensions of each context 
                i += 1;
                var contextXbrlId = contextElement.Attribute("id").Value;
                var scenario = contextElement.Element(xbrli + "scenario");


                var contextDb = new Context(DocumentId, contextXbrlId, contextXbrlId, 0);
                var sqlInsertContext = @"INSERT INTO dbo.Context (InstanceId, ContextXbrlId, Signature, TableId) VALUES (@InstanceId,@ContextXbrlId, @Signature, @TableId)
                    SELECT CAST(SCOPE_IDENTITY() as int);
                ";

                var contextId = connectionInsurance.QuerySingleOrDefault<int>(sqlInsertContext, contextDb);

                //Explicit dims //<xbrldi:explicitMember dimension="s2c_dim:AG">s2c_VM:x17</xbrldi:explicitMember>                    
                var explicitDims = scenario?.Elements(xbrldi + "explicitMember") ?? new List<XElement>();
                foreach (var explicitDim in explicitDims)
                {
                    //s2c_dim:VG(s2c_AM:x80) the result I want
                    //<xbrldi:explicitMember dimension="s2c_dim:AG">s2c_VM:x17</xbrldi:explicitMember>
                    var dimAndType = explicitDim.Attribute("dimension").Value; //s2c_dim:AG                    
                    var domainAndMember = explicitDim.Value; //s2c_VM:x17

                    //************************
                    var dd = $"{dimAndType}({domainAndMember})";
                    contextDb.ContextLinesF1.Add(dd);
                    //****************************                   
                }


                var typedDims = scenario?.Elements(xbrldi + "typedMember") ?? new List<XElement>();
                foreach (var typedDim in typedDims)
                {
                    //<xbrldi:typedMember dimension="s2c_dim:FN"><s2c_typ:ID>1</s2c_typ:ID></xbrldi:typedMember>
                    //get the domNodeValue from  the typed element(ID) -- 1 in the case above

                    var dimAndType = typedDim.Attribute("dimension").Value; //s2c_dim:AG 

                    var domNode = typedDim.Elements()?.First(); //<s2c_typ:ID>1</s2c_typ:ID>                    
                    var domain = domNode?.Name?.LocalName ?? ""; //ID
                    var domainMember = domNode.Value; //1                     
                    var domainAndMember = $"{domain}:{domainMember}";  //s2c_typ:ID

                    //******************************
                    var dd = $"{dimAndType}({domainAndMember})";
                    contextDb.ContextLinesF1.Add(dd);
                    //*****************************

                }
                var sqlUpdateContext = @"update Context set Signature=@Signature where ContextId=@ContextId;";

                contextDb.BuildSignature();
                connectionInsurance.Execute(sqlUpdateContext, new { contextDb.Signature, contextId });
                Console.Write($"^");
            }
        }

        private void AddFacts()
        {
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            //Read the facts
            //<s2md_met:ei1643 contextRef="c">s2c_CN:x12</s2md_met:ei1643>
            //<s2md_met:mi655 contextRef="AGx17_EXx13_FNID_POx60_RTx154_TAx2_VGx80" decimals="2" unitRef="u">327267425.67</s2md_met:mi655>

            //todo check if all facts are met 
            XNamespace metFactNs = "http://eiopa.europa.eu/xbrl/s2md/dict/met";

            //XbuFact.Contexts = Contexts; //it is a static property used by facts in all template Sheets
            //XbuFact.Units = Units; //it is a static property used by facts in all template Sheets
            var count = 0;
            var factElements = RootNode.Elements().Where(el => el.Name.Namespace == metFactNs).ToList();
            foreach (var fe in factElements)
            {
                // <s2md_met:mi503 contextRef="BLx119" decimals="2" unitRef="u">169866295.22</s2md_met:mi503>
                var prefix = fe.GetPrefixOfNamespace(metFactNs); //maybe not needed as an attribute

                var unitRef2 = fe.Attribute("unitRef")?.Value;
                var decimals = 0;
                try
                {
                    decimals = int.Parse(fe.Attribute("decimals")?.Value);
                }
                catch
                {
                    decimals = 0;
                }

                var unitRef = fe.Attribute("unitRef")?.Value ?? "";
                var metric = fe.Name.LocalName.ToString(); //maybe not needed in Db                
                var xbrlCode = $"{prefix.Trim()}:{metric.Trim()}";
                
                var mMetric = FindFactMetricId(xbrlCode);  //"s2md_met:ei1633"                

                var dataTypeUse = mMetric is not null ? CntConstants.SimpleDataTypes[mMetric.DataType] : ""; 
                //var dataTypeUse = CntConstants.SimpleDataTypes[mMetric.DataType]; //N, S,B,E..

                //var unitNN = XbuFact.Units.ContainsKey(unitRef) ? Units[unitRef] : unitRef;
                var contextId = fe.Attribute("contextRef")?.Value ?? "";

                //-----------------------                

                var newFact = new TemplateSheetFact
                {
                    InstanceId = DocumentId,
                    Row = "",
                    Col = "",
                    Zet = "",
                    InternalCol = 0,
                    InternalRow = 0,
                    CellID = 0,
                    CurrencyDim = "",
                    //Metric = fe.Name.LocalName.ToString(),
                    MetricID=mMetric?.MetricID??0 ,
                    //nsPrefix = prefix,
                    XBRLCode = xbrlCode,
                    ContextId = contextId,
                    Unit = unitRef,
                    Decimals = decimals,
                    IsConversionError = false,
                    IsEmpty = false,
                    TextValue = fe.Value,
                    NumericValue = 0,
                    DateTimeValue = new DateTime(1999, 12, 31),
                    BooleanValue = false,
                    DataType = mMetric?.DataType ??  "" ,
                    DataTypeUse = dataTypeUse,
                    DataPointSignature = "",
                    DataPointSignatureFilled = "",
                    RowSignature = "",
                };

                var ctxLines = GetContextLinesNew(contextId);

                newFact.UpdateFactDetails(xbrlCode, ctxLines);

                var sqlInsFact = @"
                    
INSERT INTO dbo.TemplateSheetFact (
     DataType
    ,DataTypeUse
    ,TextValue
	,NumericValue
	,DateTimeValue
	,BooleanValue
	,XBRLCode
	,DataPointSignature
	,DataPointSignatureFilled
    ,Signature
	,InstanceId
	,Row
	,Col
	,Zet
	,CellID
	,CurrencyDim	
    ,metricID
	,contextId
	,Unit
	,Decimals
	
	
	)
VALUES (
     @DataType
    ,@DataTypeUse
	,@TextValue
	,@NumericValue
	,@DateTimeValue
	,@BooleanValue
	,@XBRLCode
	,@DataPointSignature
	,@DataPointSignatureFilled
    ,@Signature
	,@InstanceId
	,@Row
	,@Col
	,@Zet
	,@CellID
	,@CurrencyDim	
    ,@metricID
	,@contextId
	,@Unit
	,@Decimals
	
	);

                    SELECT CAST(SCOPE_IDENTITY() as int);
                   ";
                try
                {
                    newFact.FactId = connectionInsurance.QuerySingleOrDefault<int>(sqlInsFact, newFact);
                    CreateFactDimsDb(ConfigObject, newFact.FactId, newFact.DataPointSignature);
                }
                catch (Exception e)
                {
                    var errMessage = $"{e.Message}===>, fact text:{newFact.TextValue}, xbrl:{newFact.XBRLCode}";
                    Console.WriteLine(errMessage);
                     Log.Error(errMessage);

                }
                

                


                Console.Write(".");

                count++;
                if (count % 1000 == 0)
                {
                    Console.WriteLine($"facts Count:{count}");
                }


                //---------------------------

                List<string> GetContextLinesNew(string contextId)
                {
                    //using var connectionInsurance = new SqlConnection(configObject.LocalDatabaseConnectionString);                
                    var sqlContext = @"select Signature from Context where ContextXbrlId= @contextId and InstanceId =@DocumentId";
                    var signature = connectionInsurance.QuerySingleOrDefault<string>(sqlContext, new { contextId, DocumentId });
                    return signature.Split("|", StringSplitOptions.RemoveEmptyEntries).ToList();

                }



            }

            MMetric FindFactMetricId(string xbrlCode)
            {
                //xbrl code is actually the metric of a fact
                using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

                var sqlMetric = @"
                SELECT met.MetricID, met.CorrespondingMemberID, met.DataType
                FROM dbo.mMetric met
                LEFT OUTER JOIN mMember mem ON mem.MemberID = met.CorrespondingMemberID
                WHERE mem.MemberXBRLCode = @xbrlCode
            ";
                var metric = connectionEiopa.QuerySingleOrDefault<MMetric>(sqlMetric, new { xbrlCode });
                return metric;
            }

        }

        internal static int CreateFactDimsDb(ConfigObject config, int factId, string signature)
        {

            using var connectionInsurance = new SqlConnection(config.LocalDatabaseConnectionString);

            var dims = signature.Split("|").ToList();
            if (dims.Count > 0)
            {
                dims.RemoveAt(0);
            }

            var count = 0;
            foreach (var dim in dims)
            {
                count++;
                var dimDom = DimDom.GetParts(dim);
                var factDim = new TemplateSheetFactDim()
                {
                    FactId = factId,
                    Dim = dimDom.Dim,
                    Dom = dimDom.Dom,
                    DomValue = dimDom.DomValue,
                    Signature = dimDom.Signature,
                    IsExplicit = true
                };
                var sqlInsDim = @"
                    INSERT INTO dbo.TemplateSheetFactDim (FactId, Dim, Dom, DomValue, Signature, IsExplicit)
                    VALUES(@FactId, @Dim, @Dom, @DomValue, @Signature, @IsExplicit)";

                connectionInsurance.Execute(sqlInsDim, factDim);
            }

            return count;
        }

        private List<DocInstance> GetExistingDocuments()
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqlExists = @"
                    select doc.InstanceId, doc.Status, EiopaVersion from DocInstance doc  where  
                    PensionFundId= @FundId and ModuleId=@moduleId
                    and ApplicableYear = @ApplicableYear and ApplicableQuarter = @ApplicableQuarter"
                    ;

            var docParams = new { FundId, ModuleId = Module.ModuleID, ApplicableYear, ApplicableQuarter };
            var docs = connectionInsurance.Query<DocInstance>(sqlExists, docParams).ToList();
            return docs;

        }

        private int DeleteDocument(int documentId)
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqlDeleteDoc = @"delete from DocInstance where InstanceId= @documentId";
            var rows = connectionInsurance.Execute(sqlDeleteDoc, new { documentId });

            var sqlErrorDocDelete = @"delete from DocInstance where InstanceId= @documentId";
            connectionInsurance.Execute(sqlErrorDocDelete, new { documentId });

            return rows;
        }


        private MModule GetModule(string moduleCode)
        {
            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            //module code : {ari, qri, ara, ...}
            var sqlModule = "select ModuleCode, ModuleId, ModuleLabel from mModule mm where mm.ModuleCode = @ModuleCode";
            var module = connectionEiopa.QuerySingleOrDefault<MModule>(sqlModule, new { moduleCode = moduleCode.ToLower().Trim() });
            if (module is null)
            {
                return new MModule();
            }
            return module;

        }


        private void WriteProcessStarted()
        {
            var message = $"XBRL Reader Started -- Fund:{FundId} ModuleId:{ModuleCode} Year:{ApplicableYear} Quarter:{ApplicableQuarter} Solvency:{SolvencyVersion} file:{FileName}";
            Console.WriteLine(message);
            Log.Information(message);
        }

        private void UpdateDocumentStatus(string status)
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqlUpdate = @"update DocInstance  set status= @status where  InstanceId= @documentId;";
            var doc = connectionInsurance.Execute(sqlUpdate, new { DocumentId, status });
        }
    }
}
