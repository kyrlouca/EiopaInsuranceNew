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
    public class TestNormalTermParser
    {
        [Fact]
        public void ValidateNormalTerm()
        {
            var normalTerm = "";

            


            normalTerm = @"{S.01.02.07.01, r0180,c0010}";
            var res1 = PlainTermParser.ParseTerm(normalTerm);
            res1.TableCode.Should().Be("S.01.02.07.01");            
            res1.Row.Should().Be("R0180");
            res1.Col.Should().Be("C0010");
            res1.IsSum.Should().BeFalse();

            normalTerm = @"{S.01.02.04.01, c0010}";
            var res2 = PlainTermParser.ParseTerm(normalTerm);
            res2.TableCode.Should().Be("S.01.02.04.01");            
            res2.Row.Should().Be("");
            res2.Col.Should().Be("C0010");
            res2.IsSum.Should().BeFalse();

            normalTerm = @"{S.16.01.01.02, r0200}";
            var res3 = PlainTermParser.ParseTerm(normalTerm);
            res3.TableCode.Should().Be("S.16.01.01.02");            
            res3.Row.Should().Be("R0200");
            res3.Col.Should().Be("");
            res3.IsSum.Should().BeFalse();

            normalTerm = @"//{S.16.01.01.02, r0040-0190}";
            var res4 = PlainTermParser.ParseTerm(normalTerm);
            res4.TableCode.Should().Be("S.16.01.01.02");            
            res4.Row.Should().Be("R0040-0190");
            res4.Col.Should().Be("");
            res4.IsSum.Should().BeTrue();

            //this is used for technical terms when i want to get the value directly instead from the fact
            normalTerm = @"{S.01.02.07.01, r0180,c0010,val=[abc]}";
            var res0 = PlainTermParser.ParseTerm(normalTerm);
            res0.TableCode.Should().Be("S.01.02.07.01");
            res0.Row.Should().Be("R0180");
            res0.Col.Should().Be("C0010");
            res0.IsSum.Should().BeFalse();
            res0.TermValue.Should().Be("ABC");


        }
    }
}
