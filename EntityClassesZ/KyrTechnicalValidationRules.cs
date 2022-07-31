using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityClassesZ
{
    public class KyrTechnicalValidationRules
    {
        public int TechnicalValidationId { get; set; }
        public string ValidationId { get; set; }
        public string TableCode { get; set; }
        public string Rows { get; set; }
        public string Columns { get; set; }
        public string ValidationFomula { get; set; }
        public string ValidationFomulaPrep { get; set; }
        public string ErrorMessage { get; set; }
        public string Severity { get; set; }
        public string CheckType { get; set; }
        public bool IsActive { get; set; }
        public string Dimension { get; set; }
        public string Scope { get; set; }
        public string Fallback { get; set; }
    }
}
