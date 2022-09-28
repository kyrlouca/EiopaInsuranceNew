using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityClasses
{
    public class SubmissionReferenceDate
    {
        public int SubmissionReferenceDateId { get; set; }
        public int Category { get; set; }
        public int ReferenceYear { get; set; }
        public int Quarter { get; set; }
        public DateTime ReferenceDate { get; set; }
        public DateTime SubmissionDate { get; set; }
    }
}
