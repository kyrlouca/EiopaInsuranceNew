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
    public class CreateNewRuleTest
    {
        [Fact]
        public void ValidateCreateRuleTestx()
        {
            var formula = "";
         
            //invalid
            var res0 = new RuleStructure(@"{S.02.0 + {S.12.01.02.01, r0080,c0210", "");
            res0.RuleTerms.Count.Should().Be(0);
            res0.SymbolFormula.Should().Be("");

            //it has a max and a nested min => we create another term for the nested min
            formula = @"{S.23.01.01.01,r0540,c0050}=max(0,min(0.5*{S.23.01.01.01,r0580,c0010}-3*{S.23.01.01.01,r0540,c0040})) ";
            var resmm = new RuleStructure(formula);
            resmm.SymbolFormula.Should().Be("X0=max(0,min(0.5*X1-3*X2))");
            resmm.SymbolFinalFormula.Should().Be("X0=Z0");
            resmm.RuleTerms.Count.Should().Be(5);
            resmm.RuleTerms[3].TermText.Should().Be("max(0,T00)");
            resmm.RuleTerms[4].TermText.Should().Be("min(0.5*X1-3*X2)");


            //2681 plain
            var res1 =  new RuleStructure(@"{S.02.01.02.01, r0320,c0010} = {S.12.01.02.01, r0020,c0210} + {S.12.01.02.01, r0080,c0210}","");
            res1.RuleTerms.Count.Should().Be(3);
            res1.SymbolFormula.Should().Be("X0 = X1 + X2");
            res1.RuleTerms[0].TermText.Should().Be("{S.02.01.02.01, r0320,c0010}");
            res1.RuleTerms[0].TableCode.Should().Be("S.02.01.02.01");            
            res1.RuleTerms[0].Row.Should().Be("R0320");
            res1.RuleTerms[0].Col.Should().Be("C0010");





            //2594 sum
            formula = "{S.02.01.02.01,r0140,c0010}=sum({S.06.02.01.01,c0170,snnn})";
            var res2 = new RuleStructure(formula,"");
            res2.RuleTerms.Count.Should().Be(3);
            res2.SymbolFormula.Should().Be("X0=sum(X1)");
            res2.SymbolFinalFormula.Should().Be("X0=Z0");
            res2.RuleTerms[0].TermText.Should().Be("{S.02.01.02.01,r0140,c0010}");            
            res2.RuleTerms[1].TermText.Should().Be("{S.06.02.01.01,c0170,snnn}");
            res2.RuleTerms[1].TableCode.Should().Be("S.06.02.01.01");            
            res2.RuleTerms[2].TermText.Should().Be("sum(X1)");

            //2594 matches
            formula = @"matches({S.06.02.01.02,c0290},"" ^ ..1.$"") and {S.06.02.01.01,c0090}=[s2c_LB:x91]";
            var res3 = new RuleStructure(formula,"");
            res3.RuleTerms.Count.Should().Be(3);
            res3.SymbolFormula.Should().Be(@"matches(X0,"" ^ ..1.$"") and X1=[s2c_LB:x91]");
            res3.SymbolFinalFormula.Should().Be("Z0 and X1=[s2c_LB:x91]");
            res3.RuleTerms[0].TermText.Should().Be(@"{S.06.02.01.02,c0290}");
            res3.RuleTerms[0].TableCode.Should().Be(@"S.06.02.01.02");
            res3.RuleTerms[1].TermText.Should().Be(@"{S.06.02.01.01,c0090}");
            res3.RuleTerms[1].TableCode.Should().Be(@"S.06.02.01.01");
            res3.RuleTerms[2].TermText.Should().Be(@"matches(X0,"" ^ ..1.$"")");

            //2578 equalEnum
            formula = @"matches({S.06.02.01.02,c0290},"" ^ ..((3) | (4)).$"") and {S.06.02.01.02,c0310} != [s2c_PU:x16] and {S.06.02.01.01,c0090}=[s2c_LB:x91]";
            var res4 = new RuleStructure(formula,"");
            res4.RuleTerms.Count.Should().Be(4);
            res4.SymbolFormula.Should().Be(@"matches(X0,"" ^ ..((3) | (4)).$"") and X1 != [s2c_PU:x16] and X2=[s2c_LB:x91]");
            res4.RuleTerms[0].TermText.Should().Be(@"{S.06.02.01.02,c0290}");
            res4.RuleTerms[1].TermText.Should().Be(@"{S.06.02.01.02,c0310}");
            res4.RuleTerms[2].TermText.Should().Be(@"{S.06.02.01.01,c0090}");
            

            //4847 empty
            formula = @"if (not(empty({S.05.01.02.01, r0200,c0020}))) then not(empty({S.17.01.02.01, r0320,c0030}))";
            var res5 =  new RuleStructure(formula);
            res5.SymbolFormula.Should().Be(@"if (not(empty(X0))) then not(empty(X1))");
            res5.SymbolFinalFormula.Should().Be(@"if (not(Z0)) then not(Z1)");            
            res5.RuleTerms.Count.Should().Be(4);
            res5.RuleTerms[0].TermText.Should().Be(@"{S.05.01.02.01, r0200,c0020}");
            res5.RuleTerms[1].TermText.Should().Be(@"{S.17.01.02.01, r0320,c0030}");
            res5.RuleTerms[2].TermText.Should().Be("empty(X0)");
            res5.RuleTerms[3].TermText.Should().Be("empty(X1)");


            //2499 isEqualEnum
            formula = @"if ({S.01.02.01.01, r0040,c0010} != [s2c_SE:x129] and {S.01.02.01.01, r0040,c0010} != [s2c_SE:x130]) then {S.01.01.02.01, r0580,c0010} = [s2c_CN:x1]";
            var res6 = new RuleStructure(formula);
            res6.RuleTerms.Count.Should().Be(2);
            res6.RuleTerms[0].TermText.Should().Be(@"{S.01.02.01.01, r0040,c0010}");
            res6.RuleTerms[1].TermText.Should().Be(@"{S.01.01.02.01, r0580,c0010}");
            res6.SymbolFormula.Should().Be(@"if (X0 != [s2c_SE:x129] and X0 != [s2c_SE:x130]) then X1 = [s2c_CN:x1]");

            //6887 fdtv            
            formula = @"matches(ftdv({S.06.02.01.02,c0290},""s2c_dim:UI""),""^CAU/.*"") and not(matches(ftdv({S.06.02.01.02,c0290},""s2c_dim:UI""),""^CAU/(ISIN/.*)|(INDEX/.*)""))";
            var res7 = new RuleStructure(formula);
            res7.SymbolFinalFormula.Should().Be("Z0 and not(Z1)");
            var plain = res7.RuleTerms.Where(term => !term.IsFunctionTerm);
            plain.Count().Should().Be(1);//just one term, no doubles
            var fn = res7.RuleTerms.Where(term => term.IsFunctionTerm).ToArray();
            _ = fn.Length.Should().Be(4);
            fn[0].Letter.Should().Be("Z0");
            fn[0].TermText.Should().Be(@"matches(T00,""^CAU/.*"")");
            fn[2].Letter.Should().Be("T00");
            fn[3].Letter.Should().Be("T10");
            res7.RuleTerms[0].TermText.Should().Be(@"{S.06.02.01.02,c0290}");            
            res7.RuleTerms[0].Col.Should().Be("C0290");
            res7.RuleTerms[0].Row.Should().Be("");
            

            //4442 fallback 
            formula = @"not(matches({S.06.02.01.02,c0290},""^..((71)|(9.)|(09))$"")) and not(isfallback({S.06.02.01.01,c0130}))";
            var res8 = new RuleStructure(formula);
            res8.RuleTerms.Count.Should().Be(4);
            res8.SymbolFormula.Should().Be(@"not(matches(X0,""^..((71)|(9.)|(09))$"")) and not(isfallback(X1))");
            res8.SymbolFinalFormula.Should().Be(@"not(Z0) and not(Z1)");
            res8.RuleTerms[0].TermText.Should().Be(@"{S.06.02.01.02,c0290}");
            res8.RuleTerms[1].TermText.Should().Be(@"{S.06.02.01.01,c0130}");


            //3147 min
            formula = @"{S.23.01.01.01,r0540,c0050}=min(0.5*{S.23.01.01.01,r0580,c0010}-{S.23.01.01.01,r0540,c0040},0.15*{S.23.01.01.01,r0580,c0010},{S.23.01.01.01,r0500,c0050})";
            var res9 = new RuleStructure(formula);
            res9.RuleTerms.Count.Should().Be(5);
            res9.SymbolFormula.Should().Be(@"X0=min(0.5*X1-X2,0.15*X1,X3)");
            res9.SymbolFinalFormula.Should().Be(@"X0=Z0");
            
            res9.RuleTerms[0].TermText.Should().Be(@"{S.23.01.01.01,r0540,c0050}");
            res9.RuleTerms[1].TermText.Should().Be(@"{S.23.01.01.01,r0580,c0010}");
            res9.RuleTerms[2].TermText.Should().Be(@"{S.23.01.01.01,r0540,c0040}");           
            res9.RuleTerms[4].TermText.Should().Be(@"min(0.5*X1-X2,0.15*X1,X3)");

            //3141 min and max
            formula = @"{S.23.01.01.01, r0540,c0030} = min(max({S.23.01.01.01, r0540,c0020}, 0) * 0.25, {S.23.01.01.01, r0500,c0030})";
            var res10 = new  RuleStructure(formula);
            res10.RuleTerms.Count.Should().Be(5);//3 normal one Z0 and T0
            res10.SymbolFormula.Should().Be("X0 = min(max(X1, 0) * 0.25, X2)");
            res10.SymbolFinalFormula.Should().Be("X0 = Z0");            
            res10.RuleTerms[0].TermText.Should().Be(@"{S.23.01.01.01, r0540,c0030}");
            res10.RuleTerms[3].TermText.Should().Be(@"min(T00 * 0.25, X2)");//this is the Z0 term
            res10.RuleTerms[4].TermText.Should().Be(@"max(X1, 0)");//this is T0 which is inner


            //3520 ExDimVal
            formula = @"ExDimVal({S.01.01.01.01,r0520,c0010},AO)=x0 and ExDimVal({S.26.03.01.04,r0800,c0060},AO)=x1 and ExDimVal({S.26.03.01.04,r0900,c0060},AO)=x1 and ExDimVal({S.26.03.01.04,r0100,c0060},AO)=x1 and ExDimVal({S.26.03.01.04,r0200,c0060},AO)=x1 and ExDimVal({S.26.03.01.04,r0300,c0060},AO)=x1 and ExDimVal({S.26.03.01.04,r0400,c0060},AO)=x1 and ExDimVal({S.26.03.01.04,r0500,c0060},AO)=x1 and ExDimVal({S.26.03.01.04,r0600,c0060},AO)=x1 and ExDimVal({S.26.03.01.04,r0700,c0060},AO)=x1 ";
            var res11 = new  RuleStructure(formula);
            res11.RuleTerms.Count.Should().Be(20);
            res11.SymbolFinalFormula.Should().Be("Z0=x0 and Z1=x1 and Z2=x1 and Z3=x1 and Z4=x1 and Z5=x1 and Z6=x1 and Z7=x1 and Z8=x1 and Z9=x1");
            res11.RuleTerms[0].TermText.Should().Be(@"{S.01.01.01.01,r0520,c0010}");
            


            //2570	S.02.01.02.01
            var filterFormula = @"matches({S.06.02.01.02,c0290},""^..((93)|(95)|(96))$"") and ({S.06.02.01.01,c0090}=[s2c_LB:x91])";
            var res12 = new RuleStructure("",filterFormula);
            res12.FilterTerms.Count.Should().Be(3);
            res12.SymbolFilterFormula= @"matches(X1,""^..((93)|(95)|(96))$"") and (X2=[s2c_LB:x91])";
            res12.SymbolFilterFinalFormula = @"Z1";
            res12.FilterTerms[0].TermText = @"{S.06.02.01.02,c0290}";
            res12.FilterTerms[2].TermText = @"matches(X1,""^..((93)|(95)|(96))$""))";
        }


    }
}
