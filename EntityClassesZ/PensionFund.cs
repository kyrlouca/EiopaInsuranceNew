using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class PensionFund
    {
        public int id { get; set; }
        public int PensionFundId { get; set; }
        public int OrganizationCategoryId { get; set; }
        public int OrganizationStatusID { get; set; }
        public string Status { get; set; }
        public bool IsLarge { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }

    }
}
