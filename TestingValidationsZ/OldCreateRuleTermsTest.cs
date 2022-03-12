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
    public class OldCreateRuleTermsTest
    {
        [Fact]
        public void ValidateCreateRuleTestx()
        {
            var formula = "";
            

            //2681 plain
            var res1 = RuleStructure.CreateRuleTerms(@"{S.02.01.02.01, r0320,c0010} = {S.12.01.02.01, r0020,c0210} + {S.12.01.02.01, r0080,c0210}");
            res1.Count.Should().Be(3);
            res1[0].TermText.Should().Be("{S.02.01.02.01, r0320,c0010}");


            //2594 sum
            formula = "{S.02.01.02.01,r0140,c0010}=sum({S.06.02.01.01,c0170,snnn})";
            var res2 = RuleStructure.CreateRuleTerms(formula);
            res2.Count.Should().Be(2);
            res2[1].TermText.Should().Be("sum({S.06.02.01.01,c0170,snnn})");


            //2594 matches
            formula = @"matches({S.06.02.01.02,c0290},"" ^ ..1.$"") and {S.06.02.01.01,c0090}=[s2c_LB:x91]";
            var res3 = RuleStructure.CreateRuleTerms(formula);
            res3.Count.Should().Be(2);
            res3[0].TermText.Should().Be(@"matches({S.06.02.01.02,c0290},"" ^ ..1.$"")");
            res3[1].TermText.Should().Be(@"{S.06.02.01.01,c0090}=[s2c_LB:x91]");

         
            //2578 equalEnum
            formula = @"matches({S.06.02.01.02,c0290},"" ^ ..((3) | (4)).$"") and {S.06.02.01.02,c0310} != [s2c_PU:x16] and {S.06.02.01.01,c0090}=[s2c_LB:x91]";
            var res4 = RuleStructure.CreateRuleTerms(formula);
            res4.Count.Should().Be(3);
            res4[0].TermText.Should().Be(@"matches({S.06.02.01.02,c0290},"" ^ ..((3) | (4)).$"")");
            res4[1].TermText.Should().Be(@"{S.06.02.01.02,c0310} != [s2c_PU:x16]");
            res4[2].TermText.Should().Be(@"{S.06.02.01.01,c0090}=[s2c_LB:x91]");

            //4847 empty
            formula = @"if (not(empty({S.05.01.02.01, r0200,c0020}))) then not(empty({S.17.01.02.01, r0320,c0030}))";
            var res5 = RuleStructure.CreateRuleTerms(formula);
            res5.Count.Should().Be(2);
            res5[0].TermText.Should().Be(@"empty({S.05.01.02.01, r0200,c0020})");
            res5[1].TermText.Should().Be(@"empty({S.17.01.02.01, r0320,c0030})");

            //2499 isEqualEnum
            formula = @"if ({S.01.02.01.01, r0040,c0010} != [s2c_SE:x129] and {S.01.02.01.01, r0040,c0010} != [s2c_SE:x130]) then {S.01.01.02.01, r0580,c0010} = [s2c_CN:x1]";
            var res6 = RuleStructure.CreateRuleTerms(formula);
            res6.Count.Should().Be(3);
            res6[0].TermText.Should().Be(@"{S.01.02.01.01, r0040,c0010} != [s2c_SE:x129]");
            res6[1].TermText.Should().Be(@"{S.01.02.01.01, r0040,c0010} != [s2c_SE:x130]");
            res6[2].TermText.Should().Be(@"{S.01.01.02.01, r0580,c0010} = [s2c_CN:x1]");

            //6887 fdtv            
            formula = @"matches(ftdv({PF.06.02.24.02,c0230},""s2c_dim:UI""),""^CAU/.*"") and not(matches(ftdv({PF.06.02.24.02,c0230},""s2c_dim:UI""),""^((CAU/ISIN)|(CAU/INDEX)).*""))";
            var res7 = RuleStructure.CreateRuleTerms(formula);
            res7.Count.Should().Be(2);
            res7[0].TermText.Should().Be(@"matches(ftdv({PF.06.02.24.02,c0230},""s2c_dim:UI""),""^CAU/.*"")");
            res7[1].TermText.Should().Be(@"matches(ftdv({PF.06.02.24.02,c0230},""s2c_dim:UI""),""^((CAU/ISIN)|(CAU/INDEX)).*"")");


            //4442 fallback 
            formula = @"not(matches({S.06.02.01.02,c0290},""^..((71)|(9.)|(09))$"")) and not(isfallback({S.06.02.01.01,c0130}))";
            var res8 = RuleStructure.CreateRuleTerms(formula);
            res8.Count.Should().Be(2);
            res8[0].TermText.Should().Be(@"matches({S.06.02.01.02,c0290},""^..((71)|(9.)|(09))$"")");
            res8[1].TermText.Should().Be(@"isfallback({S.06.02.01.01,c0130})");


            //3147 min
            formula = @"{S.23.01.01.01,r0540,c0050}=min(0.5*{S.23.01.01.01,r0580,c0010}-{S.23.01.01.01,r0540,c0040},0.15*{S.23.01.01.01,r0580,c0010},{S.23.01.01.01,r0500,c0050})";
            var res9 = RuleStructure.CreateRuleTerms(formula);
            res9.Count.Should().Be(2);
            res9[0].TermText.Should().Be(@"{S.23.01.01.01,r0540,c0050}");
            res9[1].TermText.Should().Be(@"min(0.5*{S.23.01.01.01,r0580,c0010}-{S.23.01.01.01,r0540,c0040},0.15*{S.23.01.01.01,r0580,c0010},{S.23.01.01.01,r0500,c0050})");

            //3141 min and max
            formula = @"{S.23.01.01.01, r0540,c0030} = min(max({S.23.01.01.01, r0540,c0020}, 0) * 0.25, {S.23.01.01.01, r0500,c0030})";
            var res10 = RuleStructure.CreateRuleTerms(formula);
            res10.Count.Should().Be(2);
            res10[0].TermText.Should().Be(@"{S.23.01.01.01, r0540,c0030}");
            res10[1].TermText.Should().Be(@"min(max({S.23.01.01.01, r0540,c0020}, 0) * 0.25, {S.23.01.01.01, r0500,c0030})");

            //3520 ExDimVal
            formula = @"ExDimVal({S.01.01.01.01,r0520,c0010},AO)=x0 and ExDimVal({S.26.03.01.04,r0800,c0060},AO)=x1 and ExDimVal({S.26.03.01.04,r0900,c0060},AO)=x1 ";
            var res11 = RuleStructure.CreateRuleTerms(formula);
            res11.Count.Should().Be(6);
            res11[0].TermText.Should().Be(@"ExDimVal({S.01.01.01.01,r0520,c0010},AO)");
            
        }
        

    }
}
