using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{

    public class MTable
    {
        public int TableID { get; set; } = default;
        public string TableCode { get; set; }
        public string TableLabel { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string XbrlFilingIndicatorCode { get; set; }
        public string XbrlTableCode { get; set; }
        public int ConceptID { get; set; }
        public string YDimVal { get; set; }
        public string ZDimVal { get; set; }
        public bool IsOpenTable { get; set; } = false;
    }
}
