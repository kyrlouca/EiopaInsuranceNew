using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using XbrlReader;


namespace TestingValidationsZ
{

    public class TestXbrlReader
    {
        [Fact]
        public void TestSimp()
        {
            //var test= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)|s2c_dim:DI(s2c_DI:x5)|s2c_dim:OC(*?[237])|s2c_dim:RB(*[332;1512;0])|s2c_dim:RM(s2c_TI:x44)|s2c_dim:TB(s2c_LB:x28)|s2c_dim:VG(s2c_AM:x80)";
            //var test2 = @"MET(s2md_met:mi1104)|s2c_dim:BL(*[334;1512;0])|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(*)|s2c_dim:RD(*)|s2c_dim:RE(*)";
            var str1= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)";

            var simplified1 = FactsProcessor.SimplifyCellSignature(str1, true);
            simplified1.Should().Be(@"MET(s2md_met:mi87)|s2c_dim:AF(%)|s2c_dim:AX(%)|s2c_dim:BL(s2c_LB:x9)");

             simplified1 = FactsProcessor.SimplifyCellSignature(str1, false);
            simplified1.Should().Be(@"MET(s2md_met:mi87)|s2c_dim:AX(%)|s2c_dim:BL(s2c_LB:x9)");
        }

        ///
        [Fact]
        public void TestMakeWild()
        {
            ////@"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)";
            //var test= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)|s2c_dim:DI(s2c_DI:x5)|s2c_dim:OC(*?[237])|s2c_dim:RB(*[332;1512;0])|s2c_dim:RM(s2c_TI:x44)|s2c_dim:TB(s2c_LB:x28)|s2c_dim:VG(s2c_AM:x80)";
            //var test2 = @"MET(s2md_met:mi1104)|s2c_dim:BL(*[334;1512;0])|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(*)|s2c_dim:RD(*)|s2c_dim:RE(*)";
            
            var str1 = @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)";
            var simplified1 = FactsProcessor.MakeCellSignatureWild(str1);            
            simplified1.Should().Be(@"MET(s2md_met:mi87)%|s2c_dim:AX(%)|s2c_dim:BL(s2c_LB:x9)");

            var str2 = @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:OC(*?[237])";
            var simplified2 = FactsProcessor.MakeCellSignatureWild(str2);
            simplified2.Should().Be(@"MET(s2md_met:mi87)%%");


            var str3= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)|s2c_dim:FC(*)|s2c_dim:OC(*?[237])";
            var simplified3 = FactsProcessor.MakeCellSignatureWild(str3);
            simplified3.Should().Be(@"MET(s2md_met:mi87)%|s2c_dim:AX(%)|s2c_dim:BL(s2c_LB:x9)|s2c_dim:FC(%)%");

        }
    }
}
