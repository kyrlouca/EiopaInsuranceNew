using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityClasses
{
    
    public class Fund
    {
        public bool IsActive { get; set; }
        public string Lei { get; set; }
        public string FundName { get; set; }
        public int FundId { get; set; }
        public int Wave { get; set; }
    }
}
