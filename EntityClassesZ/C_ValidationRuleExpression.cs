using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class C_ValidationRuleExpression
    {
        public int TableId { get; set; }
        public int ValidationRuleID { get; set; }
        public int ExpressionID { get; set; }
        public string ValidationCode { get; set; }
        public string Severity { get; set; }
        public string Scope { get; set; }
        public string TableBasedFormula { get; set; }
        public string Filter { get; set; }
        public string LogicalExpression { get; set; }
        public string ErrorMessage { get; set; }
        
    }
}
