using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityClasses
{


    public class TemplateSheetFactDim
    {
        public int FactId { get; set; }
        public string Dim { get; set; }
        public string Dom { get; set; }
        public string DomValue { get; set; }
        public bool IsExplicit { get; set; }
        public string Signature { get; set; }
        public int FactDimId { get; set; }
    }
}
