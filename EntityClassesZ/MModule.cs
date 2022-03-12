using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{

    public class MModule
    {
        public int ModuleID { get; set; }
        public int TaxonomyID { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleLabel { get; set; }
        public int ConceptualModuleID { get; set; }
        public string DefaultFrequency { get; set; }
        public int ConceptID { get; set; }
        public string XBRLSchemaRef { get; set; }
        public bool IsAggregate { get; set; }
        public int OrganizationCategory { get; set; }

    }
}
