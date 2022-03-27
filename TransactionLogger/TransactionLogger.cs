using System;
using System.Collections.Generic;
using System.Text;

using Dapper;
using Microsoft.Data.SqlClient;
using ConfigurationNs;

namespace TransactionLoggerNs
{
    public enum ProgramCode { AG, DO, XB, VA,CX,RX }
    public enum ProgramAction { DEL, INS, UPD }
    public enum MessageType {ERROR,INFO,COMPLETE }

    public class TransactionLog
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

    public class TransactionLogger
    {
        public static void LogTransaction(string solvencyVer, TransactionLog tl)
        {
            var configObject = Configuration.GetInstance(solvencyVer).Data;
            using var connectionInsurance = new SqlConnection(configObject.LocalDatabaseConnectionString);
            var sqlInsert = @"
                INSERT INTO TransactionLog(PensionFundId, ModuleCode, ApplicableYear, ApplicableQuarter, Message, UserId, ProgramCode, ProgramAction,InstanceId,MessageType,FileName)
                VALUES(@PensionFundId, @ModuleCode, @ApplicableYear, @ApplicableQuarter, @Message,  @UserId, @ProgramCode, @ProgramAction,@InstanceId,@MessageType,@FileName);
            ";


            var x = connectionInsurance.Execute(sqlInsert, tl);
            //var x = connectionPension.Execute(sqlInsert, new { PensionFundId = 1, ModuleCode = "ari", ApplicableYear = 30, ApplicableMonth = 1, Message = "a", UserId = 3, OperationModule = "a", OperationAction = "b" });

        }
    }
}
