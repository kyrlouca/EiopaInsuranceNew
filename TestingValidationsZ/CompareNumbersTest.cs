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
    public class CompareNumbersTest
    {
        [Fact]
        public void TestNumbers()
        {
            bool res1;
            res1 = RuleStructure.CompareNumbers("=", 1, 100, 101);
            res1.Should().BeTrue();

            res1 = RuleStructure.CompareNumbers("=", 0.5, 100, 100.9);
            res1.Should().BeFalse();

            //--------------------------
            res1 = RuleStructure.CompareNumbers("<", 1, 100, 100.9);
            res1.Should().BeTrue();


            res1 = RuleStructure.CompareNumbers("<", 0.5, 100.9, 100.0);
            res1.Should().BeFalse();

            res1 = RuleStructure.CompareNumbers("<", 1, 100.9, 100.0);
            res1.Should().BeTrue();

            res1 = RuleStructure.CompareNumbers("<", 1, -100.0, 98);
            res1.Should().BeTrue();
            //--------------------------

            res1 = RuleStructure.CompareNumbers(">", 1, 1000, 100);
            res1.Should().BeTrue();


            res1 = RuleStructure.CompareNumbers(">", 0.5, 100, 100.9);
            res1.Should().BeFalse();

            res1 = RuleStructure.CompareNumbers(">", 1, 100, 100.9);
            res1.Should().BeTrue();

            res1 = RuleStructure.CompareNumbers(">", 1, 100, -1000);
            res1.Should().BeTrue();
            //--------------------------

        }
    }
}
