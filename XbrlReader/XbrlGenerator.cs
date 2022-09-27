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



        public static bool GenerateXbrlFile( string solvencyVersion, int currencyBatchId, int userId, int fundId,string moduleCode, int applicableYear, int applicableQuarter, string fileName)
        {            
            var isValidEiopaVersion = Configuration.IsValidVersion(solvencyVersion);
            if (!isValidEiopaVersion)
            {
                var message = $"Invalid Solvency:{solvencyVersion}";
                Console.WriteLine(message);
                    return false;
            }
            var configObject = GetConfiguration(solvencyVersion);

            //we need the module to delete the document
            var module = GetModule(configObject, moduleCode);

            if (module.ModuleID == 0)
            {
                //cannot create transactionLog because document does not exist
                var message = $"Invalid module code : {moduleCode}";
                Console.WriteLine(message);
                Log.Error(message);
                return false;
            }


            var existingDocs = GetExistingDocuments(configObject,fundId,module.ModuleID,applicableYear,applicableQuarter);

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
                .ForEach(doc => DeleteDocument(configObject, doc.InstanceId));


            //*************************************************
            //create the document anyway so we can attach log errors
            //*************************************************
            var documentId = CreateDocInstanceInDb(configObject,currencyBatchId,userId,fundId,moduleCode,applicableYear,applicableQuarter,fileName,solvencyVersion);
            if (documentId == 0)
            {
                var message = $"Cannot Create DocInstance for companyId: {fundId} year:{applicableYear} quarter:{applicableQuarter} ";
                Console.WriteLine(message);
                Log.Error(message);
                return false;

            }



            var reader = XbrlFileReader.ProcessXbrlFileNew(documentId, solvencyVersion, currencyBatchId, userId, fundId, moduleCode, applicableYear, applicableQuarter, fileName);
            if (reader is null)
            {
                return false;
            }
            FactsProcessor.ProcessFactsAndAssignToSheets(reader.SolvencyVersion, reader.DocumentId, reader.FilingsSubmitted);

            var diffminutes = DateTime.Now.Subtract(reader.StartTime).TotalMinutes;
            Log.Information($"XbrlFileReader Minutes:{diffminutes}");
            return true;

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


        static private MModule GetModule(ConfigObject configObject, string moduleCode)
        {
            using var connectionPension = new SqlConnection(configObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(configObject.EiopaDatabaseConnectionString);

            //module code : {ari, qri, ara, ...}
            var sqlModule = "select ModuleCode, ModuleId, ModuleLabel from mModule mm where mm.ModuleCode = @ModuleCode";
            var module = connectionEiopa.QuerySingleOrDefault<MModule>(sqlModule, new { moduleCode = moduleCode.ToLower().Trim() });
            if (module is null)
            {
                return new MModule();
            }
            return module;

        }


        static private int CreateDocInstanceInDb(ConfigObject configObject,int currencyBatchId, int userId, int fundId, string  moduleCode, int applicableYear, int applicableQuarter, string fileName, string solvencyVersion)
        {
            using var connection = new SqlConnection(configObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(configObject.EiopaDatabaseConnectionString);


            var module = GetModule(configObject, moduleCode);

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
                ModuleId = module.ModuleID,
                fileName,
                currencyBatchId,
                Status = "P",
                EiopaVersion = solvencyVersion,
            };


            var result = connection.QuerySingleOrDefault<int>(sqlInsertDoc, doc);            
            return result;
        }



    }
}
