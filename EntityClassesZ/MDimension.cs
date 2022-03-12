using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class MDimension
    {
        //domain code should be fetched from joining Domain table        
        public int DimensionID { get; set; }
        public string DimensionLabel { get; set; }
        public string DimensionCode { get; set; }
        public string DimensionDescription { get; set; }
        public string DimensionXBRLCode { get; set; }
        public int DomainID { get; set; }
        public string DomainCode { get; set; }//domain code should be fetched from joining Domain table
        public bool IsTypedDimension { get; set; }
        public int ConceptID { get; set; }
    }
}
