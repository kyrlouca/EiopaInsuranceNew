using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class DocInstance
    {
        public int InstanceId { get; set; }
        public int PensionFundId { get; set; }
        public int UserId { get; set; }
        public string ModuleCode { get; set; } = "";
        public int ApplicableYear { get; set; }
        public int ApplicableQuarter { get; set; }
        public int ModuleId { get; set; }
        public DateTime TimestampCreate { get; set; } = new DateTime(1900, 01, 01);
        public string EntityCurrency { get; set; } = "";
        public string Status { get; set; } = "";
        public bool IsSubmitted { get; set; } = false;
        public string FileName { get; set; } = "";
        public string EiopaVersion { get; set; } = "";
        public int CurrencyBatchId { get; set; } = 0;


    }


}
