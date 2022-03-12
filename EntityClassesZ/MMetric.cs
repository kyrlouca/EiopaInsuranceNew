using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class MMetric
    {
        public int MetricID { get; set; }
        public int CorrespondingMemberID { get; set; }
        public string DataType { get; set; }        
        public string FlowType { get; set; }
        public string BalanceType { get; set; }
        public int ReferencedDomainID { get; set; }
        public int ReferencedHierarchyID { get; set; }
        public int HierarchyStartingMemberID { get; set; }
        public bool IsStartingMemberIncluded { get; set; }
    }
}
