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

namespace TestingValidationsZ
{
    public class ValidateRuleStructuresZ
    {
        public static string SolvencyVersion { get; internal set; } = "TEST250";
        public static int DocumentId { get; internal set; } = 6007;
        public ConfigObject ConfigObject { get; private set; }

        public ValidateRuleStructuresZ()
        {
            ConfigObject = Configuration.GetInstance(SolvencyVersion).Data;
        }

        [Fact]
        public void ValidateCreateRuleStructureFromDb()
        {
            var selectRule = @"
        SELECT
	          vr.ValidationRuleID		 
 	         ,vr.ExpressionID	
	          ,vr.ValidationCode
              ,vr.Severity
              ,vr.Scope            
	          ,ex.TableBasedFormula
	          ,ex.Filter
	          ,ex.LogicalExpression      
          FROM vValidationRule vr 
          join vExpression ex on ex.ExpressionID= vr.ExpressionID          
          where   
            vr.ValidationRuleID= @ValidationRuleId
        ";

            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);


            var rule = connectionEiopa.QuerySingleOrDefault<C_ValidationRuleExpression>(selectRule, new { ValidationRuleId = 6745 });
            var res = new RuleStructure(rule);

            //{PF.04.03.24.01, c0040} = {PF.04.03.24.01, c0010} + {PF.04.03.24.01, c0020}            
            Assert.Equal(3, res.RuleTerms.Count);



            //res2.UpdateTermsWithScopeRowCol(ApplicableAxis.Rows, "r", "0022");
            //res2.RuleTerms.First().CellRecordDetailsToDelete.row.Should().Be("R0022");


         
        }



        [Fact]
        public void ValidateRuleStructureFromText()
        {
            //rule 577
            var rule1 = @"{PFE.02.01.30.01, r0280} = {PF.29.05.24.01, r0060}";
            var ruleTerms = RuleStructure.CreateRuleTerms(rule1);
            //var term1 = new RuleTerm("$X0", @"{PFE.02.01.30.01, r0280}");
            //var term2 = new RuleTerm("$X1", @"{PF.29.05.24.01, r0060}");
            // RuleTerm term3;
            //term1.Should().BeEquivalentTo(ruleTerms[0]);
            //term2.Should().BeEquivalentTo(ruleTerms[1]);




            rule1 = @"not(isfallback({PF.08.01.24.02,c0340}))";
            ruleTerms = RuleStructure.CreateRuleTerms(rule1);
            //term1 = new RuleTerm("$X0", @"isfallback({PF.08.01.24.02,c0340})");          
            //term1.Should().BeEquivalentTo(ruleTerms[0]);
          


            rule1 = @"{PFE.02.01.30.01,r0200,c0040} = sum({PFE.06.02.30.01,c0100,snnn})";
            ruleTerms = RuleStructure.CreateRuleTerms(rule1);
            //term1 = new RuleTerm("$X0", @"{PFE.02.01.30.01,r0200,c0040}");
            //term2 = new RuleTerm("$X1", @"sum({PFE.06.02.30.01,c0100,snnn})");
            ////term1.Should().BeEquivalentTo(ruleTerms[0]);
            //term2.Should().BeEquivalentTo(ruleTerms[1]);



        }

    }
}
