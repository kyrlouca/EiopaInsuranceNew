using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{

    public class TemplateSheetJoinDocument
    {

        public int TemplateSheetId { get; set; }
        public int InstanceId { get; set; }
        public string SheetCode { get; set; }
        public int TableID { get; set; }
        public string ZDimVal { get; set; }
        public string YDimVal { get; set; }
        public string XbrlFilingIndicatorCode { get; set; }
        public int ModuleId { get; set; }
        public string DocumentType { get; set; }

    }
}
