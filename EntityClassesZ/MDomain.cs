using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class MDomain
    {
        public int DomainID { get; set; }
        public string DomainCode { get; set; }
        public string DomainLabel { get; set; }
        public string DomainDescription { get; set; }
        public string DomainXBRLCode { get; set; }
        public string DataType { get; set; }
        public bool IsTypedDomain { get; set; }
        public int ConceptID { get; set; }
    }
}
