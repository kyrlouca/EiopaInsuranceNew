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
        public int DocumentId { get; internal set; }
        public List<string> FilingsSubmitted { get; set; } = new();        
        public Dictionary<string, string> Units { get; protected set; } = new Dictionary<string, string>();

        public string SolvencyVersion { get; internal set; }

        public XElement RootNode { get; private set; }
        readonly XNamespace xbrli = "http://www.xbrl.org/2003/instance";
        readonly XNamespace xbrldi = "http://xbrl.org/2006/xbrldi";
        readonly XNamespace xlink = "http://www.w3.org/1999/xlink";
        readonly XNamespace link = "http://www.xbrl.org/2003/linkbase";
        readonly XNamespace typedDimNs = "http://eiopa.europa.eu/xbrl/s2c/dict/typ";
        readonly XNamespace findNs = "http://www.eurofiling.info/xbrl/ext/filing-indicators";


        public static void MatchFacts(ConfigObject configObject, string signature)
        {
            using var connectionEiopa = new SqlConnection(configObject.EiopaDatabaseConnectionString);
            using var connectionInsurance = new SqlConnection(configObject.LocalDatabaseConnectionString);
            var dims = signature.Split("|").ToList();


        }

        public XbrlFileReader()
        {
            Console.WriteLine("ONLY for testing XbrlData");
            return;
        }

        public XbrlFileReader(string solvencyVersion, int currencyBatchId, int userId, int fundId, int applicableYear, int applicableQuarter, string fileName)
        {
            //Read an Xbrl file and store the data in structures (dictionary of units, contexs, facts)
            //Then store document, sheets, and facts in database
            SolvencyVersion = solvencyVersion;
            CurrencyBatchId = currencyBatchId;
            UserId = userId;
            FundId = fundId;
            ApplicableYear = applicableYear;
            ApplicableQuarter = applicableQuarter;
            FileName = fileName;
            

            if (!GetConfiguration())
            {
                return;
            }

            IsValidEiopaVersion = Configuration.IsValidVersion(SolvencyVersion);

            WriteProcessStarted();
            //var data = new XbrlDataProcessor(SolvencyVersion, this);            
            //return;

            XmlDoc = CreateXbrlData(fileName);
            
            var diffminutes = StartTime.Subtract(DateTime.Now).TotalMinutes;
            Log.Information($"XbrlFileReader Minutes:{diffminutes}");
        }



        private bool GetConfiguration()
        {

            if (!Configuration.IsValidVersion(SolvencyVersion))
            {
                var errorMessage = $"XbrlFileReader --Invalid Eiopa Version: {SolvencyVersion}";
                Console.WriteLine(errorMessage);
                Log.Error(errorMessage);
                return false;
            }

            ConfigObject = Configuration.GetInstance(SolvencyVersion).Data;
            if (string.IsNullOrEmpty(ConfigObject.LoggerXbrlFile))
            {
                var errorMessage = "LoggerXbrlFile is not defined in ConfigData.json";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(ConfigObject.LoggerXbrlReaderFile, rollOnFileSizeLimit: true, shared: true, rollingInterval: RollingInterval.Day)
            .CreateLogger();


            if (string.IsNullOrEmpty(ConfigObject.EiopaDatabaseConnectionString) || string.IsNullOrEmpty(ConfigObject.LocalDatabaseConnectionString))
            {
                var errorMessage = "Empty ConnectionStrings";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }


            return true;
        }

        private XDocument CreateXbrlData(string sourceFile)
        {
            //Parse an xbrl file and create on object of the class which has the contexts, facts, etc
            //However, with the new design design, contexts and facts are saved in memory tables and NOT in data structures


            if (!File.Exists(sourceFile))
            {
                var message = $"XBRL CreateXbrlDocument ERROR: Document not Found : {sourceFile}";
                Console.WriteLine($"**** {message}");
                Log.Error(message);
                return null;
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
                return null;
            }

            RootNode = xmlDoc.Root;

            var reference = RootNode.Element(link + "schemaRef").Attribute(xlink + "href").Value;
            ModuleCode = GeneralUtils.GetRegexSingleMatch(@"http.*mod\/(\w*)", reference);

            Console.WriteLine($"Opened Xblrl=>  Module: {ModuleCode} ");

            DocumentId = CreateDocInstanceInDb();
            
            //AddFilingIndicators();
            AddValidFilingIndicators();
            Console.WriteLine("filing Indicators");

            Console.WriteLine("\nCreate Units");
            AddUnits();

            Console.WriteLine("\nCreate Contexts");
            AddContexts();

            

            Console.WriteLine("\nCreate Facts");
            AddFacts();

            DeleteContexts();
            return xmlDoc;



        }

        private void DeleteContexts()
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);            
            connectionInsurance.Execute("Delete from Context where InstanceId= @DocumentId", new {DocumentId });
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

        //private void AddFilingIndicators()
        //{
        //    //filing indicators
        //    var filingsHeader = RootNode.Element(findNs + "fIndicators");
        //    var filingIndicators = filingsHeader?.Elements(findNs + "filingIndicator").ToList();
        //    foreach (var fi in filingIndicators)
        //    {
        //        var isNotFiled = fi.Attribute(findNs + "filed")?.Value == "false";
        //        var val = fi.Value;
        //        FilingIndicators.Add(new XbuFilingIndicator(val, !isNotFiled));
        //    }
        //}

        private int CreateDocInstanceInDb()
        {
            using var connection = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            var sqlInsertDoc = @"
               INSERT INTO  DocInstance
                   (                                            
                    [PensionFundId]                   
                   ,[UserId]                   
                   ,[ModuleCode]           
                   ,[ApplicableYear]
                   ,[ApplicableQuarter]                   
                   ,[ModuleId]      
                   ,[FileName]
                   ,[CurrencyBatchId]
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
                ModuleId =moduleId,
                FileName,
                CurrencyBatchId
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
                var xbrlCode= $"{prefix.Trim()}:{metric.Trim()}";
                var mMetric = FindFactMetricId(xbrlCode);  //"s2md_met:ei1633"              
                var dataTypeUse = CntConstants.SimpleDataTypes[mMetric.DataType]; //N, S,B,E..

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
                    Metric = fe.Name.LocalName.ToString(),
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
                    DataType = mMetric.DataType,
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
	,metric	
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
	,@metric	
	,@contextId
	,@Unit
	,@Decimals
	
	);

                    SELECT CAST(SCOPE_IDENTITY() as int);
                   ";
                newFact.FactId = connectionInsurance.QuerySingleOrDefault<int>(sqlInsFact, newFact);
                CreateFactDimsDb(ConfigObject, newFact.FactId, newFact.DataPointSignature);

                
                Console.Write(".");

                count++;
                if(count % 1000 == 0)
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
                SELECT met.CorrespondingMemberID, met.DataType
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



        private void WriteProcessStarted()
        {
            var message = $"XBRL Reader Started -- Fund:{FundId} ModuleId:{ModuleCode} Year:{ApplicableYear} Quarter:{ApplicableQuarter} Solvency:{SolvencyVersion} file:{FileName}";
            Console.WriteLine(message);
            Log.Information(message);

            TransactionLog trans;
            trans = new TransactionLog()
            {
                PensionFundId = FundId,
                ModuleCode = "XX",
                ApplicableYear = ApplicableYear,
                ApplicableQuarter = ApplicableQuarter,
                Message = message,
                UserId = UserId,
                ProgramCode = ProgramCode.XB.ToString(),
                ProgramAction = ProgramAction.INS.ToString(),
                InstanceId = 0,
                MessageType = MessageType.INFO.ToString()
            };
            TransactionLogger.LogTransaction(SolvencyVersion, trans);
        }


    }
}
