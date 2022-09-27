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
using System.Reflection.Metadata.Ecma335;

namespace AdhocTesting
{
    internal enum ValidStatus { A, Q1, Q2, Q3, Q4 };
    public class Program
    {
        public static readonly string SolvencyVersion = "IU260";

        enum Fts { exp, count, empty, isfallback, min, max, sum, matches, ftdv, ExDimVal };
        static void Main(string[] args)
        {
            var confObject = Configuration.GetInstance(SolvencyVersion).Data;
            
            var sig0 = @"MET(s2md_met:mi503)|s2c_dim:BI(s2c_GA:x6)|s2c_dim:BL(s2c_LB:x79|s2c_dim:DI(s2c_DI:x5)|s2c_dim:EE(s2c_GA:x74)|s2c_dim:IZ(s2c_RT:x1)|s2c_dim:LG(*[290;882;0])|s2c_dim:TB(s2c_LB:x28)|s2c_dim:VG(s2c_AM:x84)";
            var sig1 = @"MET(s2md_met:mi503)|s2c_dim:BL(*[364;1521;0])|s2c_dim:DI(s2c_DI:x5)|s2c_dim:IZ(s2c_RT:x1)|s2c_dim:LA(*?[307])|s2c_dim:LR(s2c_GA:x14)|s2c_dim:TZ(s2c_LB:x163)|s2c_dim:VG(s2c_AM:x84)";
            var sig = @"MET(s2md_met:mi289)|s2c_dim:AF(*?[61])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[343;1521;0])|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(*?[242])|s2c_dim:RM(s2c_TI:x42)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";
            var sig2 = @"MET(s2md_met:mi503)|s2c_dim:BL(s2c_LB:x140)|s2c_dim:DI(s2c_DI:x5)|s2c_dim:IZ(s2c_RT:x1)|s2c_dim:LA(s2c_GA:x24)|s2c_dim:TZ(s2c_LB:x175)|s2c_dim:VG(s2c_AM:x84)";
            
            var sig5 = @"MET(s2md_met:mi414)|s2c_dim:TK(s2c_TF:x4)|s2c_dim:TX(s2c_EL:x28)|s2c_dim:VG(s2c_AM:x80)";
            var sig6 = @"MET(s2md_met:si1589)|s2c_dim:SU(s2c_MC:x171)|s2c_dim:UI(*)|s2c_dim:XG(*)";
            var sig7 = @"MET(s2md_met:mi289)|s2c_dim:AF(*?[61])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[343;1521;0])|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(*?[242])|s2c_dim:RM(s2c_TI:x42)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";
            var sig8 = @"MET(s2md_met:mi294)|s2c_dim:VG(s2c_AM:x80)";

            //var oldProcx = FactsProcessor.FindMatchingFactsRegex(confObject, 12905, sig1);

            long t0 = DateTime.Now.Ticks;
            var newProc = FactsProcessor.FindFactsFromSignatureWild(confObject, 12905, sig8);
            long t1 = DateTime.Now.Ticks;

            var oldProc = FactsProcessor.FindMatchingFactsRegex(confObject, 12905, sig8);
            long t2 = DateTime.Now.Ticks;
            if(newProc.Count != oldProc.Count)
            {
                var axxc = Console.ReadLine();
            }

            double d1 = t1 - t0; // / 1000
            double d2 = t2 - t1; // / 1000


            return;
               
            var test= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)|s2c_dim:DI(s2c_DI:x5)";
            var bxb = FactsProcessor.SimplifyCellSignature(test,true);

            var bxo = FactsProcessor.SimplifyCellSignature(test, false);

            var axx = new List<string>() { "ab", "bc" };
            var sel = axx.FirstOrDefault(item => item == "bsc");
            if(sel is null)
            {
                Console.WriteLine("b");
            }

            
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
