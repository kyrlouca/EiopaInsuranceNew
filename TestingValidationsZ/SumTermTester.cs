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
    public class SumTermTester
    {
        [Fact]
        public void ValidateSumTerm()
        {
            //BV35-1:  sum({S.16.01.01.02, r0040-0190})
            //BV45-1: sum({S.17.01.01.01, c0020-0130})            
            // BV309_2-10: sum({SR.27.01.01.20, c1300, (r3300-3600)})
            //BV252_2-10:  sum({SR.17.01.01.01, r0260, (c0020-0170)})                        
            // BV254_1-2-7: sum({S.25.01.01.01,r0010-0070,c0040})

            var res1 =SumTermParser.ParseTerm(@"sum({S.16.01.01.02, r0040-0190})");
            res1.RangeAxis.Should().Be(VldRangeAxis.Rows);
            res1.StartRowCol.Should().Be("R0040");
            res1.EndRowCol.Should().Be("R0190");
            res1.FixedRowCol.Should().Be("");
            res1.TableCode.Should().Be("S.16.01.01.02");


            var res2 = SumTermParser.ParseTerm(@" sum({S.17.01.01.01, c0020-0130})");
            res2.RangeAxis.Should().Be(VldRangeAxis.Cols);
            res2.StartRowCol.Should().Be("C0020");
            res2.EndRowCol.Should().Be("C0130");
            res2.FixedRowCol.Should().Be("");
            res2.TableCode.Should().Be("S.17.01.01.01");

            var res3 = SumTermParser.ParseTerm(@"sum({SR.27.01.01.20, c1300, (r3300-3600)})");
            res3.RangeAxis.Should().Be(VldRangeAxis.Rows);
            res3.StartRowCol.Should().Be("R3300");
            res3.EndRowCol.Should().Be("R3600");
            res3.FixedRowCol.Should().Be("C1300");
            res3.TableCode.Should().Be("SR.27.01.01.20");

            var res4 = SumTermParser.ParseTerm(@"sum({SR.17.01.01.01, r0260, (c0020-0170)})");
            res4.RangeAxis.Should().Be(VldRangeAxis.Cols);
            res4.StartRowCol.Should().Be("C0020");
            res4.EndRowCol.Should().Be("C0170");
            res4.FixedRowCol.Should().Be("R0260");
            res4.TableCode.Should().Be("SR.17.01.01.01");

            var res5 = SumTermParser.ParseTerm(@"sum({S.25.01.01.01,r0010-0070,c0040})");
            res5.RangeAxis.Should().Be(VldRangeAxis.Rows);
            res5.StartRowCol.Should().Be("R0010");
            res5.EndRowCol.Should().Be("R0070");
            res5.FixedRowCol.Should().Be("C0040");
            res5.TableCode.Should().Be("S.25.01.01.01");

            var res6 = SumTermParser.ParseTerm(@"sum({S.25.01,0010-0070,R0040})");
            res6.RangeAxis.Should().Be(VldRangeAxis.None);
            res6.StartRowCol.Should().Be("");
            res6.EndRowCol.Should().Be("");
            res6.FixedRowCol.Should().Be("");
            res6.TableCode.Should().Be("");
        }

    }
}
