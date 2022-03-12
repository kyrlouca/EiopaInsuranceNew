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
    
    public class TestCreateFunctionTermsNew
    {
        [Fact]
        public void ValidateCreateFunctionTerms()
        {
            //function terms NOT normal terms
            //Do not test nested Terms here-- use other test

            var formulax = "{S.02.01.02.01,r0140,c0010}=sum({S.06.02.01.01,c0170,snnn})";
            var (newFormulax, newTermsx) = RuleStructure.PrepareFunctionTermsNew(formulax, "Z");
            newTermsx[0].FunctionType.Should().Be(FunctionTypes.SUM);


            var formula = "X0 + X1 + empty(X1)";
            var (newFormula,newTerms) = RuleStructure.PrepareFunctionTermsNew(formula,"Z");
            newFormula.Should().Be("X0 + X1 + Z0");
            newTerms[0].FunctionType.Should().Be( FunctionTypes.EMPTY);
            


            //matches(X1,""^((XL)|(XT))..$"")
            formula = @"X0 + sum(X2) + empty(X1) + matches(X1,""^((XL)|(XT))..$"")";
            (newFormula, newTerms) = RuleStructure.PrepareFunctionTermsNew(formula, "Z");
            newFormula.Should().Be("X0 + Z0 + Z1 + Z2");
            newTerms[1].TermText.Should().Be("empty(X1)");
            newTerms[2].TermText.Should().Be(@"matches(X1,""^((XL)|(XT))..$"")");
            newTerms[2].FunctionType.Should().Be(FunctionTypes.MATCHES);


            

            formula = @"if (matches({S.26.01.04.03,r0012,c0010},""^((1)|(2)|(1,2))$"") or {S.26.01.04.03,r0020,c0010}=[s2c_AP:x33] or {S.26.01.04.03,r0030,c0010}=[s2c_AP:x33] or matches({S.27.01.04.27,r0002,c0001}, ""^((1)|(2)|(3)|(4)|(5)|(1,2)|(1,3)|(1,4)|(1,5)|(2,3)|(2,4)|(2,5)|(3,4)|(3,5)|(4,5)|(1,2,3)|(1,2,4)|(1,2,5)|(1,3,4)|(1,3,5)|(1,4,5)|(2,3,4)|(2,3,5)|(2,4,5)|(3,4,5)|(1,2,3,4)|(1,2,3,5)|(1,2,4,5)|(1,3,4,5)|(2,3,4,5)|(1,2,3,4,5))$"")) then {S.01.01.04.01,r0560,c0010}=[s2c_CN:x1] or {S.01.01.04.01,r0560,c0010}=[s2c_CN:x60] or {S.01.01.04.01,r0560,c0010}=[s2c_CN:x71]";
            (newFormula, newTerms) = RuleStructure.PrepareFunctionTermsNew(formula, "Z");
            newFormula.Should().Be(@"if (Z0 or {S.26.01.04.03,r0020,c0010}=[s2c_AP:x33] or {S.26.01.04.03,r0030,c0010}=[s2c_AP:x33] or Z1) then {S.01.01.04.01,r0560,c0010}=[s2c_CN:x1] or {S.01.01.04.01,r0560,c0010}=[s2c_CN:x60] or {S.01.01.04.01,r0560,c0010}=[s2c_CN:x71]");
        }

    }
}
