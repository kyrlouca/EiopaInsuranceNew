using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class MTableCell
    {
        public int CellID { get; set; }
        public int TableID { get; set; }
        public string OrdinateID { get; set; }
        public string DatapointSignature { get; set; }
        public string DPS { get; set; }
        public string NoOpenDPS { get; set; }        
        public bool IsShaded { get; set; }
        public bool IsRowKey { get; set; }

    }
}
