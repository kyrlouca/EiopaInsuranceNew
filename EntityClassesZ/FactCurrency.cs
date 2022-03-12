using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityClasses
{
    public class FactCurrency
    {
        public string Currency { get; set; }
        public decimal ExchangeRate { get; set; }
        public int FactId { get; set; }
    }
}
