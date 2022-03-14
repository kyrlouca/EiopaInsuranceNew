using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Validations;

namespace TestingValidationsZ
{

    public class TestAssertExpression
    {
        [Fact]
        public void ValidatedFixExpression()
        {
            var theTerm = new RuleTerm("X0", "abc", 9, false, DateTime.Now, EiopaConstants.DataTypeMajorUU.StringDtm, false);
            var theExp= "";
            var rules = new List<RuleTerm>();
            if (rules.Count == 0)
            {

            }

           
            

                        

            //adding terms
            theExp = "X0 + X1 <=X2";
            rules = new List<RuleTerm>( )
            {
                new RuleTerm("X0", "has val", 3, false, DateTime.Now, EiopaConstants.DataTypeMajorUU.NumericDtm,false),
                new RuleTerm("X1", "has val", 6, false, DateTime.Now, EiopaConstants.DataTypeMajorUU.NumericDtm,false),
                new RuleTerm("X2", "has val", 9, false, DateTime.Now, EiopaConstants.DataTypeMajorUU.NumericDtm,false),                
            };
            var res4 = (bool)RuleStructure.AssertExpression(0, theExp, rules);
            res4.Should().BeTrue();


            //if then 
            theExp = "if(not(X0)) then (not(X1))";
            rules = new List<RuleTerm>()
            {
                new RuleTerm("X0", "has val", 0, false, DateTime.Now, EiopaConstants.DataTypeMajorUU.BooleanDtm,false),
                new RuleTerm("X1", "has val", 0, false, DateTime.Now, EiopaConstants.DataTypeMajorUU.BooleanDtm,false),
                
            };
            var res5 = (bool)RuleStructure.AssertExpression(0, theExp, rules);
            res5.Should().BeTrue();            


        }
    }
}
