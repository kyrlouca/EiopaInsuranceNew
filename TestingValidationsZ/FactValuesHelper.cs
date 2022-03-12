using System;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using Dapper;
using Microsoft.Data.SqlClient;
using EntityClasses;
using System.Linq;
using Validations;
using ConfigurationNs;



namespace TestingValidations
{
    class FactValuesHelper
    {
        public static string SolvencyVersion { get; internal set; } = "V250";

        private static int GetDocument(int fundId, string moduleCode, string sheetCode)
        {
            var sqlGetDoc = @"
                select doc.InstanceId from DocInstance doc  join TemplateSheetInstance sheet on sheet.InstanceId=doc.InstanceId 
                where SheetCode= @sheetCode and doc.PensionFundId=@FundId and doc.ModuleCode =@ModuleCode;
            ";

            var ConfigObjectNew = Configuration.GetInstance(SolvencyVersion).Data;
            using var connectionPension = new SqlConnection( ConfigObjectNew.EiopaDatabaseConnectionString);

            var doc = connectionPension.QuerySingleOrDefault<int>(sqlGetDoc, new { fundId, moduleCode, sheetCode });
            return doc;
        }

    }
}
