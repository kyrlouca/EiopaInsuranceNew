using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityClassesZ
{
    public class SubmissionDate
    {
        public int Category { get; set; }
        public DateTime Q1 { get; set; }
        public DateTime Q2 { get; set; }
        public DateTime Q3 { get; set; }
        public DateTime Q4 { get; set; }
        public DateTime A { get; set; }
        public int ReferenceYear { get; set; }
        public int SubmissionDateId { get; set; }
    }
}
