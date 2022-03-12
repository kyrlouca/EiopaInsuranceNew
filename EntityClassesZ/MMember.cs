using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class MMember
    {
        public int MemberID { get; set; }
        public int DomainID { get; set; }
        public string MemberCode { get; set; }
        public string MemberLabel { get; set; }
        public string MemberXBRLCode { get; set; }
        public bool IsDefaultMember { get; set; }
        public int ConceptID { get; set; }
    }
}
