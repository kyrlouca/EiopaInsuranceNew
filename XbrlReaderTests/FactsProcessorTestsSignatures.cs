using XbrlReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace XbrlReader.Tests
{
   
    public class FactsProcessorTestsSignatures
    {       
        [Fact]
        public void SimplifyCellSignatureTestSignature()
        {
            var test= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)|s2c_dim:OC(*?[237])|s2c_dim:RB(*[332;1512;0])|s2c_dim:RM(s2c_TI:x44)";
            var xx = FactsProcessor.SimplifyCellSignature(test,true);
            xx.Should().Be(@"MET(s2md_met:mi87)|s2c_dim:AF(%)|s2c_dim:AX(%)|s2c_dim:BL(s2c_LB:x9)|s2c_dim:OC(%)|s2c_dim:RB(%)|s2c_dim:RM(s2c_TI:x44)");
        }
    }
}