using EiopaConstants;
using EntityClasses;
using EntityClassesZ;
using GeneralUtilsNs;
using HelperInsuranceFunctions;
using Microsoft.Data.SqlClient;
using Dapper;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Schema;
using TransactionLoggerNs;
using System.Reflection.PortableExecutable;
using System.Xml;
using System.Globalization;
using System.Security.Policy;
using Shared.Services;

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

        public IConfigObject ConfigObjectR { get; private set; }
        public ConfigData ConfigDataR { get; private set; }

        public XDocument XmlDoc { get; private set; }
        public int UserId { get; private set; }
        public int FundId { get; private set; }
        public int ApplicableYear { get; private set; }
        public int ApplicableQuarter { get; private set; }
        public string FileName { get; private set; }


        public string ModuleCode { get; private set; }
        public int ModuleId { get; private set; }

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



        public static bool StarterStatic(string solvencyVersion, int currencyBatchId, int userId, int fundId, string moduleCode, int applicableYear, int applicableQuarter, string fileName)
        {
            //Creates the instance and then calls its functions

            if (!ConfigObject.IsValidVersion(solvencyVersion))
            {
                var message = $"Invalid Solvency:{solvencyVersion}";
                Console.WriteLine(message);
                return false;
            }
            var configObjectNew = HostCreator.CreateTheHost(solvencyVersion);


            //we need the module to delete the document
            var module = InsuranceData.GetModuleByCodeNew(configObjectNew, moduleCode);

            if (module.ModuleID == 0)
            {
                //cannot create transactionLog because document does not exist
                var message = $"Invalid module code : {moduleCode}";
                Console.WriteLine(message);
                Log.Error(message);
                return false;
            }

            var xr = new XbrlFileReader(configObjectNew, 1, solvencyVersion, currencyBatchId, userId, fundId, moduleCode, applicableYear, applicableQuarter, fileName);

            var existingDocs = xr.GetExistingDocuments();

            var isLockedDocument = existingDocs.Any(doc => doc.Status.Trim() == "P" || doc.IsSubmitted);
            if (isLockedDocument)
            {
                var existingDoc = existingDocs.First();
                var existingDocId = existingDoc.InstanceId;
                var status = existingDoc.Status.Trim();

                var message = $"Cannot create Document with Id: {existingDoc.InstanceId}. The document has already been Submitted";
                if (status == "P")
                {
                    message = $"Cannot create Document with Id: {existingDoc.InstanceId}. The Document is currently being processed with status :{existingDoc.Status}";
                }
                Log.Error(message);
                Console.WriteLine(message);

                var trans = new TransactionLog()
                {
                    PensionFundId = fundId,
                    ModuleCode = moduleCode,
                    ApplicableYear = applicableYear,
                    ApplicableQuarter = applicableQuarter,
                    Message = message,
                    UserId = userId,
                    ProgramCode = ProgramCode.RX.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = existingDoc.InstanceId,
                    MessageType = MessageType.ERROR.ToString()
                };

                TransactionLogger.LogTransaction(solvencyVersion, trans);
                return false;
            }


            //delete older versions (except from locked or submitted)
            existingDocs.Where(doc => doc.Status.Trim() != "P" && !doc.IsSubmitted)
                .ToList()
                .ForEach(doc => xr.DeleteDocument( doc.InstanceId));


            //*************************************************
            //create the document anyway so we can attach log errors
            //*************************************************
            var documentId = xr.CreateDocInstanceInDb( currencyBatchId, userId, fundId, moduleCode, applicableYear, applicableQuarter, fileName);
            if (documentId == 0)
            {
                var message = $"Cannot Create DocInstance for companyId: {fundId} year:{applicableYear} quarter:{applicableQuarter} ";
                Console.WriteLine(message);
                Log.Error(message);
                return false;

            }

            //*************************************************
            //create sheets and loose facts in Db
            xr.CreateXbrlDataInDb();

            //*************************************************
            //map facts and assign theme to sheets
            FactsProcessor.ProcessFactsAndAssignToSheets(configObjectNew, xr.DocumentId, xr.FilingsSubmitted);

            //var diffminutes = DateTime.Now.Subtract(re.StartTime).TotalMinutes;
            var diffminutes = 0;
            Log.Information($"XbrlFileReader Minutes:{diffminutes}");
            return true;

        }


        

        
        private bool CreateXbrlDataInDb()
        {
            WriteProcessStarted();


            //*************************************************
            //Parse the Xbrl File as XML
            //*************************************************
            XmlDoc = ParseXmlFile();

            if (XmlDoc is null)
            {
                return false;
            }


            var is_Test_debug = false;
#if DEBUG
            is_Test_debug = true;
#endif            
            //is_Test_debug = false;

            //****************************************************
            //* check if the fund in xbrl is the same as fund submited by user  

            var fundLei = GetXmlElementFromXbrl(XmlDoc, "si1899");
            var fundFromDb = GetDbFundByLei(ConfigDataR, fundLei);


            if (!is_Test_debug)
            {
                var fundIdDb = fundFromDb?.FundId ?? -1;
                if (fundIdDb != FundId)
                {
                    //var message = $"Fund Specified by User fundId:{reader?.FundId} Different than Fund in Xbrl lei: {factWithLei?.XBRLCode} ";
                    var message = $"The license number used:{FundId} is incorrect.";
                    Log.Error(message);
                    Console.WriteLine(message);
                    UpdateDocumentStatus("E");
                    IsValidProcess = false;


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
                        InstanceId = DocumentId,
                        MessageType = MessageType.ERROR.ToString()
                    };
                    TransactionLogger.LogTransaction(SolvencyVersion, trans);
                    return false;
                }
            }


            var fundCategory = fundFromDb?.Wave ?? -1;

            //*******************************************************
            //* Check if the reference date is the same as the xbrl reference date
            if (!is_Test_debug || 1 == 2)
            {
                var errorMessageDate = "";
                DateTime xbrlDate;
                var referenceDateObject = GetSubmissionReferenceDate(ConfigDataR, fundCategory, ApplicableYear, ApplicableQuarter);
                var xbrlDateStr = GetXmlElementFromXbrl(XmlDoc, "di1043");

                if (!DateTime.TryParseExact(xbrlDateStr, "yyyy-MM-dd", null, DateTimeStyles.None, out xbrlDate))
                {                 
                    errorMessageDate = $"Xbrl file does not have a valid Reference Date :{xbrlDateStr}";
                }
                else if (referenceDateObject is null)
                {
                    errorMessageDate = $"Reference date Record does NOT exist in Database for categery: {fundCategory}, year:{ApplicableYear}, quarter:{ApplicableQuarter}";
                }
                else if (xbrlDate != referenceDateObject.ReferenceDate)
                {
                    errorMessageDate = $"Xbrl date : {xbrlDate} different than validation Date :{referenceDateObject.ReferenceDate:dd:MM:yyyy}";
                }


                if (!string.IsNullOrEmpty(errorMessageDate))
                {
                    Log.Error(errorMessageDate);
                    Console.WriteLine(errorMessageDate);
                    UpdateDocumentStatus("E");
                    IsValidProcess = false;

                    var trans = new TransactionLog()
                    {
                        PensionFundId = FundId,
                        ModuleCode = ModuleCode,
                        ApplicableYear = ApplicableYear,
                        ApplicableQuarter = ApplicableQuarter,
                        Message = errorMessageDate,
                        UserId = UserId,
                        ProgramCode = ProgramCode.RX.ToString(),
                        ProgramAction = ProgramAction.INS.ToString(),
                        InstanceId = DocumentId,
                        MessageType = MessageType.ERROR.ToString()
                    };
                    TransactionLogger.LogTransaction(SolvencyVersion, trans);
                    return false;
                }


            }

            //****************************************************
            //* check if the document was submited after the validation date  

            if (!is_Test_debug && UserId != 1)// no check for fund id when testing
            {
                var SubmissionDateObject = GetSubmissionReferenceDate(ConfigDataR, fundCategory, ApplicableYear, ApplicableQuarter);

                var errorMessageG = string.Empty;
                if (SubmissionDateObject is null)
                {
                    errorMessageG = $"Reference date Record does NOT exist in Database for categery: {fundCategory}, year:{ApplicableYear}, quarter:{ApplicableQuarter}";
                }
                else if (DateTime.Today > SubmissionDateObject.SubmissionDate)
                {
                    errorMessageG = $"Document was submitted on {DateTime.Today:dd/MM/yyyy} which is after Last Valid Date {SubmissionDateObject.SubmissionDate:dd/MM/yyyy}";
                }


                if (!string.IsNullOrEmpty(errorMessageG))
                {
                    Log.Error(errorMessageG);
                    Console.WriteLine(errorMessageG);
                    UpdateDocumentStatus("E");
                    IsValidProcess = false;


                    var trans = new TransactionLog()
                    {
                        PensionFundId = FundId,
                        ModuleCode = ModuleCode,
                        ApplicableYear = ApplicableYear,
                        ApplicableQuarter = ApplicableQuarter,
                        Message = errorMessageG,
                        UserId = UserId,
                        ProgramCode = ProgramCode.RX.ToString(),
                        ProgramAction = ProgramAction.INS.ToString(),
                        InstanceId = DocumentId,
                        MessageType = MessageType.ERROR.ToString()
                    };
                    TransactionLogger.LogTransaction(SolvencyVersion, trans);
                    return false;
                }
            }


            //*************************************************
            //***Create loose facts not assigned to sheets
            //*************************************************
            var (isValidFacts, errorMessage) = CreateLooseFacts();
            if (!isValidFacts)
            {
                var message = errorMessage;
                Log.Error(message);
                Console.WriteLine(message);
                //Update status
                UpdateDocumentStatus("E");
                IsValidProcess = false;

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
                    InstanceId = DocumentId,
                    MessageType = MessageType.ERROR.ToString()
                };
                TransactionLogger.LogTransaction(SolvencyVersion, trans);

                return false;
            }
            

            var diffminutes = DateTime.Now.Subtract(StartTime).TotalMinutes;
            Log.Information($"XbrlFileReader Minutes:{diffminutes}");



            return true;

        }

        private XbrlFileReader(IConfigObject configObject, int documentId, string solvencyVersion, int currencyBatchId, int userId, int fundId, string moduleCode, int applicableYear, int applicableQuarter, string fileName)
        {
            //Read an Xbrl file and store the data in structures (dictionary of units, contexs, facts)
            //Then store document, sheets, and facts in database
            DocumentId = documentId;
            SolvencyVersion = solvencyVersion;
            CurrencyBatchId = currencyBatchId;
            UserId = userId;
            FundId = fundId;
            ModuleCode = moduleCode;
            ApplicableYear = applicableYear;
            ApplicableQuarter = applicableQuarter;
            FileName = fileName;


        
            if (ConfigObjectR is null)
            {
                return;
            }
            ConfigObjectR = configObject;

            //IsValidEiopaVersion = Shared.Services.ConfigObject.IsValidVersion(SolvencyVersion);

            Module = GetModule(ModuleCode);
            if (Module.ModuleID == 0)
            {
                var message = $"Invalid Module code {ModuleCode}";
                Log.Error(message);
                Console.WriteLine(message);
                return;
            }
            ModuleId = Module.ModuleID;
            ///

        }

        private static TemplateSheetFact GetFactByXbrl(ConfigData configObject, int documentId, string xbrlCode)
        {
            using var connectionLocal = new SqlConnection(configObject.LocalDatabaseConnectionString);
            var SqlfactWithLei = "select   fact.TemplateSheetId, fact.Row,fact.Col,fact.TextValue  from TemplateSheetFact fact where fact.InstanceId=@documentId and fact.XBRLCode=@XbrlCode";

            var factWithLei = connectionLocal.QueryFirstOrDefault<TemplateSheetFact>(SqlfactWithLei, new { documentId, xbrlCode });
            return factWithLei;
        }

        private static Fund GetDbFundByLei(ConfigData configObject, string lei)
        {
            using var connectionLocal = new SqlConnection(configObject.LocalDatabaseConnectionString);

            if (lei == null)
                return null;

            lei = lei.Replace(@"LEI/", "");//lei = "LEI/2138003JRMGVH8CGUR42"            
            var sqlFund = "select  fnd.FundId, fnd.FundName, fnd.IsActive, fnd.Lei , fnd.Wave from Fund fnd where fnd.Lei=@Lei";
            var fund = connectionLocal.QuerySingleOrDefault<Fund>(sqlFund, new { lei });
            return fund;
        }
        private XbrlFileReader()
        {
            Console.WriteLine("ONLY for testing XbrlData");
            return;
        }

        private XDocument ParseXmlFile()
        {
            XDocument xmlDoc;
            if (!File.Exists(FileName))
            {
                var message = $"XBRL CreateXbrlDocument ERROR: Document not Found : {FileName}";
                Console.WriteLine($"**** {message}");
                Log.Error(message);


                UpdateDocumentStatus("E");
                IsValidProcess = false;

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
                    InstanceId = 3,
                    MessageType = MessageType.ERROR.ToString()
                };
                TransactionLogger.LogTransaction(SolvencyVersion, trans);


                return null;
            }

            using (TextReader sr = File.OpenText(FileName))  //utf-8 stream

                try
                {
                    xmlDoc = XDocument.Load(sr);
                }
                catch (Exception e)
                {
                    var message = $" ERROR Cannot parse XBRL file : {FileName}";
                    Log.Error(message);
                    Log.Error(e.Message);
                    Console.WriteLine(e);

                    UpdateDocumentStatus("E");
                    IsValidProcess = false;

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
                        InstanceId = 3,
                        MessageType = MessageType.ERROR.ToString()
                    };
                    TransactionLogger.LogTransaction(SolvencyVersion, trans);

                    return null;
                }
            return xmlDoc;

        }

        private (bool, string) CreateLooseFacts()
        {
            //Parse an xbrl file and create on object of the class which has the contexts, facts, etc
            //However, with the new design design, contexts and facts are saved in memory tables and NOT in data structures            

            if (DocumentId == 0 || XmlDoc is null)
            {
                return (false, "");
            }


            RootNode = XmlDoc.Root;

            var reference = RootNode.Element(link + "schemaRef").Attribute(xlink + "href").Value;
            var moduleCodeXbrl = GeneralUtils.GetRegexSingleMatch(@"http.*mod\/(\w*)", reference);
            if (moduleCodeXbrl != Module.ModuleCode)
            {
                var message = @$"The Module Code in the Xbrl file is ""{moduleCodeXbrl}"" instead of ""{Module.ModuleCode}""";
                Log.Error(message);
                Console.WriteLine(message);

                return (false, message);
            }

            Console.WriteLine($"Opened Xblrl=>  Module: {moduleCodeXbrl} ");


            AddValidFilingIndicators();
            Console.WriteLine("filing Indicators");

            Console.WriteLine("\nCreate Units");
            AddUnits();

            Console.WriteLine("\nCreate Contexts");
            AddContexts();

            Console.WriteLine("\nCreate Facts");
            AddFacts();

            DeleteContexts();
            return (true, "");

        }

        private void DeleteContexts()
        {
            using var connectionInsurance = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);
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
            using var connectionInsurance = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);




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
            using var connectionEiopa = new SqlConnection(ConfigDataR.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);

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
                    MetricID = mMetric?.MetricID ?? 0,
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
                    DataType = mMetric?.DataType ?? "",
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
                    CreateFactDimsDb(ConfigDataR, newFact.FactId, newFact.DataPointSignature);
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
                using var connectionEiopa = new SqlConnection(ConfigDataR.EiopaDatabaseConnectionString);

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

        internal static int CreateFactDimsDb(ConfigData config, int factId, string signature)
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
            using var connectionInsurance = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);
            var sqlExists = @"
                    select doc.InstanceId, doc.Status, doc.IsSubmitted, EiopaVersion from DocInstance doc  where  
                    PensionFundId= @FundId and ModuleId=@moduleId
                    and ApplicableYear = @ApplicableYear and ApplicableQuarter = @ApplicableQuarter"
                    ;

            var docParams = new { FundId, ModuleId = Module.ModuleID, ApplicableYear, ApplicableQuarter };
            var docs = connectionInsurance.Query<DocInstance>(sqlExists, docParams).ToList();
            return docs;

        }

        private int DeleteDocument(int documentId)
        {
            using var connectionInsurance = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);
            var sqlDeleteDoc = @"delete from DocInstance where InstanceId= @documentId";
            var rows = connectionInsurance.Execute(sqlDeleteDoc, new { documentId });

            var sqlErrorDocDelete = @"delete from DocInstance where InstanceId= @documentId";
            connectionInsurance.Execute(sqlErrorDocDelete, new { documentId });

            return rows;
        }

        private MModule GetModule(string moduleCode)
        {
            using var connectionPension = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigDataR.EiopaDatabaseConnectionString);

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
            var message = $"XBRL Reader Started -- Insurance Company:{FundId} ModuleId:{ModuleCode} Year:{ApplicableYear} Quarter:{ApplicableQuarter} Solvency:{SolvencyVersion} file:{FileName}";
            Console.WriteLine(message);
            Log.Information(message);
        }

        private void UpdateDocumentStatus(string status)
        {
            using var connectionInsurance = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);
            var sqlUpdate = @"update DocInstance  set status= @status where  InstanceId= @documentId;";
            var doc = connectionInsurance.Execute(sqlUpdate, new { DocumentId, status });
        }



        static SubmissionReferenceDate GetSubmissionReferenceDate(ConfigData confObject, int category,  int referenceYear, int quarter)
        {
            using var connectionInsurance = new SqlConnection(confObject.LocalDatabaseConnectionString);
            
            var sqlSubDate = @"
                SELECT
                  srd.SubmissionReferenceDateId
                 ,srd.Category
                 ,srd.ReferenceYear
                 ,srd.ReferenceDate
                 ,srd.SubmissionDate
                 ,srd.Quarter
                FROM dbo.SubmissionReferenceDate srd
                WHERE srd.Category = @category
                AND srd.ReferenceYear = @referenceYear
                AND srd.Quarter = @quarter

                ";
            var sRecord = connectionInsurance.QueryFirstOrDefault<SubmissionReferenceDate>(sqlSubDate, new { referenceYear, category, quarter });

            return sRecord;


        }


        static string GetXmlElementFromXbrl(XDocument xDoc, string xbrlCode)
        {
            //XNamespace ns = "http://CalculatorService/";
            //var html = xml.Descendants(ns + "html").ToList();

            //<s2md_met:si1899 contextRef="c0">LEI/2138006PEHZTJLNAPC69</s2md_met:si1899>  
            XNamespace metFactNs = "http://eiopa.europa.eu/xbrl/s2md/dict/met";
            var leiVal = xDoc.Root.Descendants(metFactNs + xbrlCode).FirstOrDefault()?.Value ?? "";
            return leiVal;
        }


        private int CreateDocInstanceInDb(int currencyBatchId, int userId, int fundId, string moduleCode, int applicableYear, int applicableQuarter, string fileName)
        {
            using var connection = new SqlConnection(ConfigDataR.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigDataR.EiopaDatabaseConnectionString);


            //var module = InsuranceData.GetModuleByCodeNew(ConfigObject, moduleCode);

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




            var doc = new
            {
                PensionFundId = fundId,
                userId,
                moduleCode,
                applicableYear,
                applicableQuarter,
                ModuleId = ModuleId,
                fileName,
                currencyBatchId,
                Status = "P",
                EiopaVersion = "xx",
            };


            var result = connection.QuerySingleOrDefault<int>(sqlInsertDoc, doc);
            return result;
        }



    }



}
