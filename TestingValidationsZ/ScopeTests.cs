//using Microsoft.VisualStudio.TestTools.UnitTesting;
using Validations;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
//using Assert = Xunit.Assert;
using System.Linq;

namespace Validations.Tests
{
    
    public class ScopeTests
    {
        [Fact]
        public void ScopeDetailsTest()
        {
            var xx = ScopeDetails.Parse(@"PFE.01.02.30.01");
            xx.ScopeAxis.Should().Be(ScopeRangeAxis.None);
            xx.ScopeRowCols.Count.Should().Be( 0);
            


            var cols = ScopeDetails.Parse(@"PFE.01.02.30.01 (c0010)");
            cols.ScopeAxis.Should().Be(ScopeRangeAxis.Cols);
            cols.ScopeRowCols.Count.Should().Be(1);
            cols.ScopeRowCols[0].Should().Be("C0010");
            

            var a3 =  ScopeDetails.Parse(@"S.23.01.01.01 (c0010;0040;0050)");
            a3.TableCode.Should().Be("S.23.01.01.01");
            a3.ScopeAxis.Should().Be(ScopeRangeAxis.Cols);
            a3.ScopeRowCols.Count.Should().Be(3);
            a3.ScopeRowCols[0].Should().Be("C0010");
            a3.ScopeRowCols[1].Should().Be("C0040");


            var a4 = ScopeDetails.Parse(@"S.27.01.01.03 (c0140-0150;0170-0200)");
            a4.TableCode.Should().Be("S.27.01.01.03");
            a4.ScopeAxis.Should().Be(ScopeRangeAxis.Cols);
            a4.ScopeRowCols[0].Should().Be("C0140");
            a4.ScopeRowCols[1].Should().Be("C0150");            
            a4.ScopeRowCols[2].Should().Be("C0170");
            a4.ScopeRowCols[3].Should().Be("C0180");

            var a5 = ScopeDetails.Parse(@"S.27.01.01.03 (z0140)");
            a5.TableCode.Should().Be("S.27.01.01.03");
            a5.ScopeAxis.Should().Be(ScopeRangeAxis.Error);
            a5.ScopeRowCols.Count.Should().Be(0);            

        }


        [Fact]
        public void ScopeDetailsTestToMove()
        {

            var rows = ScopeDetails.Parse(@"S.17.01.01.01 (r0010;0050;0060;0100;0110-0150;0160;0200-0280;0290-0310;0320-0340;0370-0440;0460-0490)");
            rows.ScopeAxis.Should().Be(ScopeRangeAxis.Rows);            
            rows.ScopeRowCols[0].Should().Be("R0010");
            rows.ScopeRowCols[1].Should().Be("R0050");
            
            rows.ScopeRowCols[4].Should().Be("R0110");
            rows.ScopeRowCols[5].Should().Be("R0120");

        }


    }
}