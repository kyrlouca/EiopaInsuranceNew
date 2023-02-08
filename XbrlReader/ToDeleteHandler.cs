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
    public class ToDeleteXbrlHandler
    {






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
