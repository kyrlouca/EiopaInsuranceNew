﻿using System;
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
        static bool OddTableCodeSelector(string tableCode)
        {
            //retruns true if last part of tablecode is odd // "S.19.01.01.05"=> true because "05" is odd
            var match = RegexConstants.TableCodeRegExP.Match(tableCode);
            if (match.Success)
            {
                // "S.19.01.01.05"=> "05"
                var lastDigits = match.Groups[2].Captures
                    .Select(cpt => cpt.Value.Substring(1))
                    .ToArray()[3];

                return int.Parse(lastDigits) % 2 != 0;

            }
            return false;
        }


        static string ModifyTableCode(string tableCode)
        {
            //retruns true if last part of tablecode is odd // "S.19.01.01.05"=> true because "05" is odd
            var match = RegexConstants.TableCodeRegExP.Match(tableCode);
            if (match.Success)
            {
                // "S.19.01.01.05"=> "05"
                var lastDigits = match.Groups[2].Captures
                    .Select(cpt => cpt.Value[1..])
                    .ToArray();

                var incDigit = int.Parse(lastDigits[3]) + 1;
                var modCode = $"{match.Groups[1].Value}.{lastDigits[0]}.{lastDigits[1]}.{lastDigits[2]}.{incDigit:D2}";
            }
            return tableCode;
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
