using ConfigurationNs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using TransactionLoggerNs;
using ConfigurationNs;
using EntityClasses;



namespace XbrlReader
{
    public class XbrlGenerator
    {



        public static void GenerateXbrlFile(ConfigObject configObject, string solvencyVersion, int currencyBatchId, int userId, int fundId,int moduleId, string moduleCode, int applicableYear, int applicableQuarter, string fileName)
        {
            var isValidProcess = true;
            var isValidEiopaVersion = Configuration.IsValidVersion(solvencyVersion);
            var reader = new XbrlFileReader(solvencyVersion, currencyBatchId, userId, fundId, moduleCode, applicableYear, applicableQuarter, fileName);


            var existingDocs = GetExistingDocuments(configObject,fundId,moduleId,applicableYear,applicableQuarter);

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
                isValidProcess = false;
                return;
            }


            //delete older versions (except from locked or submitted)
            existingDocs.Where(doc => doc.Status.Trim() != "P" && !doc.IsSubmitted)
                .ToList()
                .ForEach(doc => DeleteDocument(configObject, doc.InstanceId));

            

            //*************************************************
            //create the document anyway so we can attach log errors
            //*************************************************
            reader.CreateDocInstanceInDb();


            //*************************************************
            //Parse the Xbrl File as XML
            //*************************************************
            (var isValidXml, var parseMessage) = reader.ParseXmlFile();

            if (!isValidXml)
            {
                return;
            }

            var is_Test_debug = false;
#if DEBUG
            is_Test_debug = true;
#endif

            //****************************************************
            //* check if the fund in xbrl is the same as fund submited by user  

            var fundLei = GetXmlElementFromXbrl(reader.XmlDoc, "si1899");
            var fundFromXml = GetFundByLei(reader.ConfigObject, fundLei);


            if (!is_Test_debug)
            {
                var fundIdNew = fundFromXml?.FundId ?? -1;
                if (fundIdNew != reader.FundId)
                {
                    //var message = $"Fund Specified by User fundId:{reader?.FundId} Different than Fund in Xbrl lei: {factWithLei?.XBRLCode} ";
                    var message = $"The license number used:{reader?.FundId} is incorrect.";
                    Log.Error(message);
                    Console.WriteLine(message);
                    reader.UpdateDocumentStatus("E");
                    isValidProcess = false;


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
                        InstanceId = reader.DocumentId,
                        MessageType = MessageType.ERROR.ToString()
                    };
                    TransactionLogger.LogTransaction(solvencyVersion, trans);
                    return;
                }
            }


            var fundCategory = fundFromXml.Wave;
            //****************************************************
            //* check if the document was submited after the validation date  

            if (!is_Test_debug && reader.UserId != 1)// no check for fund id when testing
            {
                var lastValidDate = GetLastSubmissionDate(reader.ConfigObject, fundCategory, reader.ApplicableQuarter, reader.ApplicableYear);

                var errorMessageG = string.Empty;
                if (lastValidDate is null || DateTime.Today > lastValidDate)
                {
                    errorMessageG = $"Document was submitted on {DateTime.Today:dd/MM/yyyy} which is after Last Valid Date {lastValidDate?.ToString("dd/MM/yyyy")}";
                }

                if (applicableYear < DateTime.Today.Year - 1)
                {
                    errorMessageG = $"Document Reference Year: {applicableYear} is in the past";

                }


                if (!string.IsNullOrEmpty(errorMessageG))
                {
                    Log.Error(errorMessageG);
                    Console.WriteLine(errorMessageG);
                    reader.UpdateDocumentStatus("E");
                    reader.IsValidProcess = false;


                    var trans = new TransactionLog()
                    {
                        PensionFundId = reader.FundId,
                        ModuleCode = reader.ModuleCode,
                        ApplicableYear = reader.ApplicableYear,
                        ApplicableQuarter = reader.ApplicableQuarter,
                        Message = errorMessageG,
                        UserId = reader.UserId,
                        ProgramCode = ProgramCode.RX.ToString(),
                        ProgramAction = ProgramAction.INS.ToString(),
                        InstanceId = reader.DocumentId,
                        MessageType = MessageType.ERROR.ToString()
                    };
                    TransactionLogger.LogTransaction(solvencyVersion, trans);

                    return;

                }



            }


            //*************************************************
            //***Create loose facts not assigned to sheets
            //*************************************************
            var (isValidFacts, errorMessage) = reader.CreateLooseFacts();
            if (!isValidFacts)
            {
                var message = errorMessage;
                Log.Error(message);
                Console.WriteLine(message);
                //Update status
                reader.UpdateDocumentStatus("E");
                reader.IsValidProcess = false;

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
                    InstanceId = reader.DocumentId,
                    MessageType = MessageType.ERROR.ToString()
                };
                TransactionLogger.LogTransaction(solvencyVersion, trans);

                return;
            }

            FactsProcessor.ProcessFactsAndAssignToSheets(reader.SolvencyVersion, reader.DocumentId, reader.FilingsSubmitted);

            var diffminutes = DateTime.Now.Subtract(reader.StartTime).TotalMinutes;
            Log.Information($"XbrlFileReader Minutes:{diffminutes}");


        }


        private static ConfigObject GetConfiguration(string solvencyVersion)
        {

            if (!Configuration.IsValidVersion(solvencyVersion))
            {
                var errorMessage = $"XbrlFileReader --Invalid Eiopa Version: {solvencyVersion}";
                Console.WriteLine(errorMessage);
                Log.Error(errorMessage);
                return null;
            }

            var configObject = Configuration.GetInstance(solvencyVersion).Data;
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


        static private  List<DocInstance> GetExistingDocuments(ConfigObject configObject,int fundId,int moduleId,int applicableYear,int applicableQuarter)
        {
            using var connectionInsurance = new SqlConnection(configObject.LocalDatabaseConnectionString);
            var sqlExists = @"
                    select doc.InstanceId, doc.Status, doc.IsSubmitted, EiopaVersion from DocInstance doc  where  
                    PensionFundId= @FundId and ModuleId=@moduleId
                    and ApplicableYear = @ApplicableYear and ApplicableQuarter = @ApplicableQuarter"
            ;

            var docParams = new { fundId, ModuleId = moduleId, applicableYear, applicableQuarter };
            var docs = connectionInsurance.Query<DocInstance>(sqlExists, docParams).ToList();
            return docs;

        }

        static private int DeleteDocument(ConfigObject configObject, int documentId)
        {
            using var connectionInsurance = new SqlConnection(configObject.LocalDatabaseConnectionString);
            var sqlDeleteDoc = @"delete from DocInstance where InstanceId= @documentId";
            var rows = connectionInsurance.Execute(sqlDeleteDoc, new { documentId });

            var sqlErrorDocDelete = @"delete from DocInstance where InstanceId= @documentId";
            connectionInsurance.Execute(sqlErrorDocDelete, new { documentId });

            return rows;
        }



    }
}
