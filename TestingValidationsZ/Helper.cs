using System;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using Dapper;
using Microsoft.Data.SqlClient;
using EntityClasses;
using System.Linq;
using ConfigurationNs;


namespace ValidationTest
{
    public  class Helper
    {
        public ConfigObject ConfigObject { get; private set; }
        public static string SolvencyVersion { get; internal set; } = "TEST250";

        private  int GetDocumentFull(int fundId, string moduleCode, string sheetCode, int year, int quarter)
        {
            var sqlGetDoc = @"
                select doc.InstanceId from DocInstance doc  join TemplateSheetInstance sheet on sheet.InstanceId=doc.InstanceId 
            where SheetCode= @sheetCode and doc.PensionFundId=@FundId 
            and doc.ModuleCode =@ModuleCode  and doc.ApplicableYear=@year and doc.ApplicableQuarter=@quarter;
            ";

            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var doc = connectionPension.QuerySingleOrDefault<int>(sqlGetDoc, new { fundId, moduleCode, sheetCode, year, quarter });
            return doc;
        }



        public  int GetDocument(int fundId, string moduleCode, string sheetCode)
        {
            var sqlGetDoc = @"
                select doc.InstanceId from DocInstance doc  join TemplateSheetInstance sheet on sheet.InstanceId=doc.InstanceId 
                where SheetCode= @sheetCode and doc.PensionFundId=@FundId and doc.ModuleCode =@ModuleCode;
            ";


            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var doc = connectionPension.QuerySingleOrDefault<int>(sqlGetDoc, new { fundId, moduleCode, sheetCode });
            return doc;
        }



        public  (int, int) SelectFact(int documentId, string sheetCode, string row, string col)
        {

            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqlSelect = @"
                SELECT TOP 1 fact.FactId, sheet.TemplateSheetId
                FROM TemplateSheetFact fact
                JOIN TemplateSheetInstance sheet ON fact.TemplateSheetId = sheet.TemplateSheetId
                WHERE sheet.InstanceId = @documentId AND SheetCode = @sheetcode AND fact.Row = @row AND fact.Col = @col;
                ";

            var (factId, sheetId) = connectionPension.QuerySingleOrDefault<(int factId, int sheetId)>(sqlSelect, new { documentId, sheetCode, row, col });
            return (factId, sheetId);
        }

        public  void UpdateFact(int factId, string value, decimal numericValue = 0)
        {
            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqlUpdateFact = @"                
                update TemplateSheetFact  set TextValue =@value, NumericValue= @numericValue where FactId= @factId";

            connectionPension.Execute(sqlUpdateFact, new { factId, value, numericValue });
        }

        public  void DeleteAllErrors()
        {
            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqldeleteAll = @"delete from ERROR_Rule";
            connectionPension.Execute(sqldeleteAll, new { });
        }
        //

    }
}
