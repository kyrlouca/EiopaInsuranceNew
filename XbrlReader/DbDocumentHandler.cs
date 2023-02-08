using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using TransactionLoggerNs;

using EntityClasses;
using Shared.Services;
using HelperInsuranceFunctions;


namespace XbrlReader
{
    public class XbrlHandler
    {



        public static bool ProcessXbrlFile( string solvencyVersion, int currencyBatchId, int userId, int fundId,string moduleCode, int applicableYear, int applicableQuarter, string fileName)
        {
            
            
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


            var existingDocs = GetExistingDocuments(configObjectNew,fundId,module.ModuleID,applicableYear,applicableQuarter);

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
                .ForEach(doc => DeleteDocument(configObjectNew, doc.InstanceId));


            //*************************************************
            //create the document anyway so we can attach log errors
            //*************************************************
            var documentId = CreateDocInstanceInDb(configObjectNew,currencyBatchId,userId,fundId,moduleCode,applicableYear,applicableQuarter,fileName,solvencyVersion);
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




        static private  List<DocInstance> GetExistingDocuments(IConfigObject configObject,int fundId,int moduleId,int applicableYear,int applicableQuarter)
        {
            using var connectionInsurance = new SqlConnection(configObject.Data.LocalDatabaseConnectionString);
            var sqlExists = @"
                    select doc.InstanceId, doc.Status, doc.IsSubmitted, EiopaVersion from DocInstance doc  where  
                    PensionFundId= @FundId and ModuleId=@moduleId
                    and ApplicableYear = @ApplicableYear and ApplicableQuarter = @ApplicableQuarter"
            ;

            var docParams = new { fundId, ModuleId = moduleId, applicableYear, applicableQuarter };
            var docs = connectionInsurance.Query<DocInstance>(sqlExists, docParams).ToList();
            return docs;

        }

        static private int DeleteDocument(IConfigObject configObject, int documentId)
        {
            using var connectionInsurance = new SqlConnection(configObject.Data.LocalDatabaseConnectionString);
            var sqlDeleteDoc = @"delete from DocInstance where InstanceId= @documentId";
            var rows = connectionInsurance.Execute(sqlDeleteDoc, new { documentId });

            var sqlErrorDocDelete = @"delete from DocInstance where InstanceId= @documentId";
            connectionInsurance.Execute(sqlErrorDocDelete, new { documentId });

            return rows;
        }







        static private int CreateDocInstanceInDb(IConfigObject configObject,int currencyBatchId, int userId, int fundId, string  moduleCode, int applicableYear, int applicableQuarter, string fileName, string solvencyVersion)
        {
            using var connection = new SqlConnection(configObject.Data.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(configObject.Data.EiopaDatabaseConnectionString);


            var module = InsuranceData.GetModuleByCodeNew(configObject, moduleCode);

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
