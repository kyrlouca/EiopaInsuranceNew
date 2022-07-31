using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Validations;
using ConfigurationNs;

namespace TestingValidationsZ.Properties
{
    public class ParseTerm
    {
        [Fact]
        public void ParseTermValidation()
        {
            var formula = "{S.23.01.01.01,r0}";
            var res0 = HelperInsuranceFunctions.TermObject.Parse(formula);
            res0.IsValid.Should().BeFalse();
            res0.TableCode.Should().BeEmpty();
            res0.Row.Should().BeEmpty();
            res0.Col.Should().BeEmpty();

             formula = "{S.23.01.01.01,r0580,c0010}";
            var res1 = HelperInsuranceFunctions.TermObject.Parse(formula);
            res1.IsValid.Should().BeTrue();
            res1.TableCode.Should().Be("S.23.01.01.01");
            res1.Row.Should().Be("R0580");
            res1.Col.Should().Be("C0010");

            //var res0 = DocumentValidator.GetMinMaxDbTerms(configObject, formula);
            //res0.Count.Should().Be(0);

            //formula = @"min(0.5*{S.23.01.01.01,r0580,c0010}-{S.23.01.01.01,r0540,c0040},0.15*{S.23.01.01.01,r0580,c0010},{S.23.01.01.01,r0500,c0050})";

            //2681 plain
            //var res1 = DocumentValidator.GetMinMaxDbTerms(configObject, formula);
            //res1.Count.Should().Be(4);
            //res1[0].Should().Be("{S.23.01.01.01,r0580,c0010}");
            //res1[1].Should().Be("{S.23.01.01.01,r0540,c0040}");
            //res1[2].Should().Be("{S.23.01.01.01,r0580,c0010}");
            //res1[3].Should().Be("{S.23.01.01.01,r0500,c0050}");            

            

        }
    }
}
