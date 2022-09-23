using System;
using GeneralUtilsNs;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Validations;
using EiopaConstants;
using ConfigurationNs;
using Microsoft.Identity.Client;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Reflection.Metadata;
using EntityClassesZ;
using XbrlReader;

namespace AdhocTesting
{
    internal enum ValidStatus { A, Q1, Q2, Q3, Q4 };
    public class Program
    {
        public static readonly string SolvencyVersion = "IU260";

        enum Fts { exp, count, empty, isfallback, min, max, sum, matches, ftdv, ExDimVal };
        static void Main(string[] args)
        {

            var test= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)|s2c_dim:DI(s2c_DI:x5)";
            var bxb = FactsProcessor.SimplifyCellSignature(test,true);

            var bxo = FactsProcessor.SimplifyCellSignature(test, false);

            var axx = new List<string>() { "ab", "bc" };
            var sel = axx.FirstOrDefault(item => item == "bsc");
            if(sel is null)
            {
                Console.WriteLine("b");
            }

            var confObject = Configuration.GetInstance(SolvencyVersion).Data;
            var xx = GetSubmissionDate(confObject, 1, 1, 2022);




            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            //var xx = SimplifiedExpression.RemoveOutsideParenthesis(@"abc+(efg)");
            //var xx = SimplifiedExpression.Create(1,null,@"X1>X2+3||X1=X3 && X4");
            //var xx = SimplifiedExpression.Process(1, null, @"X1>X2+3||(X1=X3 && X4)");
            //var x3 = SimplifiedExpression.ParseExpression(@"abcef", 0);

            //var filename = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl260\xbrl4\test.xlsx";
            //ExcelValidationErrors.CreateErrorsExcelFile(9772, filename,"W");


            var xx3 = Enumerable.Range(1, 10).ToArray();


            var bb = 3;



        }
    

        static DateTime? GetSubmissionDate(ConfigObject confObject, int category, int quarter, int referenceYear)
        {
            using var connectionInsurance = new SqlConnection(confObject.LocalDatabaseConnectionString);
            var date2000 = new DateTime(2000, 1, 1);

            var rgQuarter = new Regex(@"[0-4]");
            if (!rgQuarter.IsMatch(quarter.ToString()))
            {
                return null;
            
            }
            

            var sqlSubDate = @"
            SELECT            
              sdate.Q1             
             ,sdate.Q2
             ,sdate.Q3
             ,sdate.Q4
             ,sdate.A
             ,sdate.ReferenceYear
             ,sdate.SubmissionDateId
            FROM dbo.SubmissionDate sdate
            WHERE sdate.ReferenceYear = @referenceYear
            AND sdate.Category = @category";
            var sRecord = connectionInsurance.QueryFirstOrDefault<SubmissionDate>(sqlSubDate, new { referenceYear, category });
            if (sRecord is null)
            {
                return null;
            }
            var sDate = quarter switch
            {
                0 => sRecord.A,
                1 => sRecord.Q1,
                2 => sRecord.Q1,
                3 => sRecord.Q1,
                4 => sRecord.Q1,
                _ => date2000
            };


            return sDate == date2000 ? null : sDate;
        }


    }
}
