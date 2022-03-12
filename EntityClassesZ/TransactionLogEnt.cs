using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClassesZ
{
    public class TransactionLogEnt
    {
        public int TransactionId { get; set; }
        public int PensionFundId { get; set; }
        public string ModuleCode { get; set; }
        public int ApplicableYear { get; set; }
        public int ApplicableQuarter { get; set; }
        public string Message { get; set; }
        public DateTime TimestampCreate { get; set; }
        public int UserId { get; set; }
        public string ProgramCode { get; set; }
        public string ProgramAction { get; set; }
        public int InstanceId { get; set; }
        public string MessageType { get; set; }
        public string FileName { get; set; }
    }
}
