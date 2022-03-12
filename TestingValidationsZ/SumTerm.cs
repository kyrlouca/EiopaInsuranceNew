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
    public class SumTerm
    {
        [Fact]
        public void ValidateFixExpression()
        {
            var res1 = SumTermParser.ParseTerm(@"sum({S.16.01.01.02, r0040-0190})");
            res1.RangeAxis.Should().Be(VldRangeAxis.Rows);
            res1.StartRowCol.Should().Be("R0040");
            res1.EndRowCol.Should().Be("R0190");
            res1.FixedRowCol.Should().Be("");
            res1.TableCode.Should().Be("S.16.01.01.02");
        }
    }
}
