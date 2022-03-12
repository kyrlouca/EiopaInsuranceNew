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
using Serilog;

namespace ValidationTest
{
    public class ValidateRulesTestOld
    {
        public ConfigObject ConfigObject { get; private set; }
        public static string SolvencyVersion { get; internal set; } = "TEST250";
        public static int  DocumentId { get; internal set; } = 6007;
        public static int FundId { get; set; } = 1;
        //[Theory] ss
        //[InlineData(@"PFE.01.02.30.01")]
        //[InlineData(@"PF.01.02.26.02 (r0430;0440;0450)")]
        //[InlineData(@"PFE.50.01.30.01 (r0010;0020;0030;0040;0050;0060;0070;0080;0090)")]

        public ValidateRulesTestOld()
        {
            
            ConfigObject = Configuration.GetInstance(SolvencyVersion).Data;
            
        }


        private int GetDocumentFull(int fundId, string moduleCode, string sheetCode, int year, int quarter)
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



        private int GetDocument(int fundId, string moduleCode, string sheetCode)
        {
            var sqlGetDoc = @"
                select doc.InstanceId from DocInstance doc  join TemplateSheetInstance sheet on sheet.InstanceId=doc.InstanceId 
                where SheetCode= @sheetCode and doc.PensionFundId=@FundId and doc.ModuleCode =@ModuleCode;
            ";

            
            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var doc = connectionPension.QuerySingleOrDefault<int>(sqlGetDoc, new { fundId, moduleCode, sheetCode });
            return doc;
        }




    


        private TemplateSheetFact SelectFactNew(int documentId, string sheetCode, string row, string col)
        {

            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var sqlSelect = @"
                SELECT TOP 1 fact.FactId, fact.TextValue, fact.NumericValue, sheet.TemplateSheetId, sheet.SheetCode
                FROM TemplateSheetFact fact
                JOIN TemplateSheetInstance sheet ON fact.TemplateSheetId = sheet.TemplateSheetId
                WHERE sheet.InstanceId = @documentId AND SheetCode = @sheetcode AND fact.Row = @row AND fact.Col = @col;
                ";

            var fact = connectionPension.QuerySingleOrDefault<TemplateSheetFact>(sqlSelect, new { documentId, sheetCode, row, col });
            return fact;
        }

        private TemplateSheetFact SelectFactFirstRow(int documentId, string sheetCode, string col)
        {

            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var sqlSelect = @"
                SELECT TOP 1 fact.FactId, fact.TextValue, fact.NumericValue, sheet.TemplateSheetId, sheet.SheetCode
                FROM TemplateSheetFact fact
                JOIN TemplateSheetInstance sheet ON fact.TemplateSheetId = sheet.TemplateSheetId
                WHERE sheet.InstanceId = @documentId AND SheetCode = @sheetcode  AND fact.Col = @col;
                ";

            var fact = connectionPension.QuerySingleOrDefault<TemplateSheetFact>(sqlSelect, new { documentId, sheetCode, col });
            return fact;
        }



        private (int, int) SelectFact(int documentId, string sheetCode, string row, string col)
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

        private  void UpdateFact(int? factId=0, string value="", decimal numericValue = 0)
        {
            
            
            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            
            var sqlUpdateFact = @"                
                update TemplateSheetFact  set TextValue =@value, NumericValue= @numericValue where FactId= @factId";

            connectionPension.Execute(sqlUpdateFact, new { factId, value, numericValue });
        }
        

        [Fact]        
        public void ValidateRulesTestFallbackAndMatch6933()
        {
            //check fallback 
            //var documentId = GetDocumentFull(fund, "ari", sheetCode,2020,1);
            var documentId = DocumentId;                        

            var sheetCode = "PF.06.02.24.02";
            var ruleId = 6933;
            //scope : PF.06.02.24.02
            //isfallback({PF.06.02.24.02,c0150})
            //matches({PF.06.02.24.02,c0230},"^..((71)|(75)|(9.))$")


            //***** Valid -- value is NOT empty, but since filter fails, rule is not checked and it is valid
           
            var factA1 = SelectFactFirstRow(documentId, sheetCode, "C0150");
            UpdateFact(factA1?.FactId, "XXx");

            //fiter                         
            var factA2 = SelectFactFirstRow(documentId, "PF.06.02.24.02", "C0230");
            UpdateFact(factA2?.FactId, "ABCc");

            //valid -- value is empty, filter is invalid
            var validator1 = new DocumentValidator(SolvencyVersion,  documentId, ruleId);
            validator1.ValidateDocument(ruleId);
            //validator1.DocumentRules[0].IsValidRule.Should().BeTrue();

            //************ Valid - value is EMPTY and the filter stands
            var factV1 = SelectFactFirstRow(documentId, "PF.06.02.24.02", "C0150");
            UpdateFact(factV1?.FactId, "");

            //fiter                         
            var factV2 = SelectFactFirstRow(documentId, "PF.06.02.24.02", "C0230");
            UpdateFact(factV2?.FactId, "AB75");

            //valid 
            var validator2 = new DocumentValidator(SolvencyVersion, documentId, ruleId);
            validator2.ValidateDocument(ruleId);
            //validator2.DocumentRules[0].IsValidRule.Should().BeTrue();

            //************Invalid -- value is NOT empty and filter returns true
            var factB1 = SelectFactFirstRow(documentId, "PF.06.02.24.02", "C0150");
            UpdateFact(factB1?.FactId, "NOT EMPTY");

            //fiter                         
            var factB2 = SelectFactFirstRow(documentId, "PF.06.02.24.02", "C0230");
            UpdateFact(factB2?.FactId, "AB75");

            //valid -- value is empty, filter is invalid
            var validator3 = new DocumentValidator(SolvencyVersion, documentId, ruleId);
            validator3.ValidateDocument(ruleId);
            //validator3.DocumentRules[0].IsValidRule.Should().BeFalse();
            

            return;

            //------------valid
        }

        
        [Fact]
        public void ValidateRulesTestEmpty6438()
        {
            //check the empty function
            var fund = 1;
            var sheetCode = "PF.01.02.25.01";
            var documentId = GetDocumentFull (fund, "qri", sheetCode,2020,1);
            var ruleId = 6438;

            //scope: PF.01.02.25.01 (c0010)
            //formula: not(empty({PF.01.02.25.01, r0100}))
            //filter null

            //valid is valid (not empty)
            var factV1 = SelectFactNew(documentId, sheetCode,"R0100", "C0010");
            UpdateFact(factV1?.FactId, "NOT Empty");
            
            
            var validator1 = new DocumentValidator(SolvencyVersion, documentId, ruleId);
            validator1.ValidateDocument(ruleId);
            //validator1.DocumentRules[0].IsValidRule.Should().BeTrue();


            //invalid, value is empty
            var factV2 = SelectFactNew(documentId, sheetCode, "R0100", "C0010");
            UpdateFact(factV2?.FactId, " ");

            //fiter                         

            //valid -- value is empty, filter is invalid
            var validator2 = new DocumentValidator(SolvencyVersion, documentId, ruleId);
            validator2.ValidateDocument(ruleId);
            //validator2.DocumentRules[0].IsValidRule.Should().BeFalse();

        }

        [Fact]
        public void ValidateRulesTestSimple6566()
        {
            //check simple term 
            
            var fund =1  ;
            var sheetCode = "PF.02.01.24.01";
            var documentId = GetDocumentFull(fund, "qri", sheetCode,2020,1);
            var ruleId = 6656;
            //scope PF.02.01.24.01 (c0010;0020;0040)
            //{PF.02.01.24.01, r0030} = {PF.02.01.24.01, r0040} + {PF.02.01.24.01, r0050}
            //filter null                        

            var row = "R0030";  
            var col = "C0010";
            var factA1= SelectFactNew(documentId, sheetCode, row, col);
             UpdateFact(factA1?.FactId, "vv",(decimal)24.00);            


            var row2 = "R0040";
            var col2 = "C0010";            
            var  factA2 = SelectFactNew(documentId, sheetCode, row2, col2);
            UpdateFact(factA2?.FactId, "xx", (decimal)20.00);

            var row3 = "R0050";
            var col3 = "C0010";
            
            var factA3 = SelectFactNew(documentId, sheetCode, row3, col3);
            UpdateFact(factA3?.FactId, "xx", (decimal)4.00);

            var validator1 = new DocumentValidator(SolvencyVersion, documentId, ruleId);
            validator1.ValidateDocument(ruleId);
            //validator1.DocumentRules[0].IsValidRule.Should().BeTrue();

            
            UpdateFact(factA3?.FactId, "xx", (decimal)6.00);

            var validator2 = new DocumentValidator(SolvencyVersion,  documentId, ruleId);
            validator2.ValidateDocument(ruleId);
            //validator2.DocumentRules[0].IsValidRule.Should().BeFalse();

        }

        


        [Fact]
        public void ValidateRulesSumSnn6699()
        {
            //Pending 
            //*** how is filter rows in PF.02.01.24.02 row matched with sum rows in PF.02.01.24.01
            var sheetCode = "PF.02.01.24.01";
            var documentId = GetDocument(1, "qri", sheetCode);
            var ruleId = 6699;
            //scope PF.02.01.24.01
            //Formula: {PF.02.01.24.01,r0020,c0040} = sum({PF.06.02.24.01,c0100,snnn})
            //Filter: matches({PF.06.02.24.02,c0230},"^..((91)|(92)|(94)|(99))$")

            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            
            var factV1 = SelectFactNew(documentId, sheetCode, "R0020", "C0040");
            UpdateFact(factV1?.FactId, "");


            //fiter *** how is PF.02.01.24.02 row matched with sum rows in PF.02.01.24.02
            var factF1 = SelectFactFirstRow(documentId, "PF.02.01.24.02", "C0230");
            UpdateFact(factV1?.FactId, "AB91");

            var validator1 = new DocumentValidator(SolvencyVersion, documentId, ruleId);
            //validator1.ValidateDocument(ruleId);
            //validator1.DocumentRules[0].IsValidRule.Should().BeTrue();


        }


        [Fact]
        public void ValidateRulesTestFdtv6768()
        {
            var sheetCode = "PF.06.02.24.02";
            var documentId = GetDocument(1, "qri", sheetCode);
            var ruleId = 6768;
            //scope PF.06.02.24.02
            //matches({PF.06.02.24.02,c0230},"^((XL)|(XT))..$")           
            //matches(ftdv({PF.06.02.24.02,c0230},"s2c_dim:UI"),"^CAU/.*") and not(matches(ftdv({PF.06.02.24.02,c0230},"s2c_dim:UI"),"^((CAU/ISIN)|(CAU/INDEX)).*"))

            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);


            //***** Valid -- value matches and the filter passes
                
            var factV1 = SelectFactFirstRow(documentId, sheetCode, "C0230");
            UpdateFact(factV1?.FactId, "XLaa");  

            //fiter                         
            var factF1 = SelectFactFirstRow(documentId, "PF.06.02.24.02", "C0010"); //corresponds to the UI dimension
            UpdateFact(factF1?.FactId, "CAU/butNotISIN");
            
            var validator1 = new DocumentValidator(SolvencyVersion, documentId, ruleId);
            validator1.ValidateDocument(ruleId);
            //validator1.DocumentRules[0].IsValidRule.Should().BeTrue();


            //***** Valid -- value not match, but filter rejected

            var factV2 = SelectFactFirstRow(documentId, sheetCode, "C0230");
            UpdateFact(factV2?.FactId, "NOT MATCH");

            //fiter                         
            var factF2 = SelectFactFirstRow(documentId, "PF.06.02.24.02", "C0010"); //corresponds to the UI dimension
            UpdateFact(factF2?.FactId, "CAU/ISIN");
            
            var validator2 = new DocumentValidator(SolvencyVersion, documentId, ruleId);
            validator2.ValidateDocument(ruleId);
            //validator2.DocumentRules[0].IsValidRule.Should().BeTrue();


            //***** InValid -- value not match when filter  passes
            var factV3 = SelectFactFirstRow(documentId, sheetCode, "C0230");
            UpdateFact(factV3?.FactId, "No match");

            //fiter                         
            var factF3 = SelectFactFirstRow(documentId, "PF.06.02.24.02", "C0010"); //corresponds to the UI dimension
            UpdateFact(factF3?.FactId, "CAU/but not ISIN");

            
            var validator3 = new DocumentValidator(SolvencyVersion, documentId, ruleId);
            validator3.ValidateDocument(ruleId);
            //validator3.DocumentRules[0].IsValidRule.Should().BeFalse();

        }

        }
    }
