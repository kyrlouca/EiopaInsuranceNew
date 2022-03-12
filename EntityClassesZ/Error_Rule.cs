using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{


    public class ERROR_Rule
    {
        public int ErrorId { get; set; }
        public int RuleId { get; set; }
        public int ErrorDocumentId { get; set; }
        public int SheetId { get; set; }
        public string SheetCode { get; set; }
        public string RowCol { get; set; }
        public string RuleMessage { get; set; }
        public bool IsError { get; set; }
        public bool IsWarning { get; set; }
        public bool IsDataError { get; set; }
        public string Row { get; set; }
        public string Col { get; set; }
        public string DataValue { get; set; }
        public string DataType { get; set; }
        public string TableBaseFormula { get; set; }
        public string Filter { get; set; }
        public string Scope { get; set; }
    }



}
