using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class ΜTemplateOrTable
    {
        public int TemplateOrTableID { get; set; }
        public int TaxonomyID { get; set; }
        public string TemplateOrTableCode { get; set; }
        public string TemplateOrTableLabel { get; set; }
        public string TemplateOrTableType { get; set; }
        public int Order { get; set; }
        public int Level { get; set; }
        public int ParentTemplateOrTableID { get; set; }
        public int ConceptID { get; set; }
        public string TC { get; set; }
        public string TT { get; set; }
        public string TL { get; set; }
        public string TD { get; set; }
        public string YC { get; set; }
        public string XC { get; set; }
    }

}
