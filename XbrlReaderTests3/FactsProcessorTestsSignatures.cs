using Microsoft.VisualStudio.TestTools.UnitTesting;
using XbrlReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace XbrlReader.Tests
{

    public class FactsProcessorTestsSignatures
    {
        
        [Fact]
        public void SimplifyCellSignatureTestSignature()
        {
            var x = 2;
            x.Should().Be(2);
        }

        
    }
}