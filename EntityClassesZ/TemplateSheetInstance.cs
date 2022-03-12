using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class TemplateSheetInstance
    {

        public int TemplateSheetId { get; set; }
        public int InstanceId { get; set; }
        public string SheetCode { get; set; }
        public string SheetTabName { get; set; }
        public string TableCode { get; set; }        
        public int TableID { get; set; }
        public string UserId { get; set; }
        public DateTime DateCreated { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string YDimVal { get; set; }
        public string ZDimVal { get; set; }        
        public string YDimValFromExcel { get; set; }
        public string ZDimValFromExcel { get; set; }        
        public string YDimFilled { get; set; }
        public string ZDimFilled { get; set; }        
        public string XbrlFilingIndicatorCode{get;set;}
        public bool IsOpenTable { get; set; }        
        public int OpenRowCounter { get; set; }
        
    }
}
