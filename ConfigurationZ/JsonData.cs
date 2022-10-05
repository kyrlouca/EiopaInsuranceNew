using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConfigurationNs
{

    public class JsonDataClass
    {
        public string BackendDatabaseConnectionString { get; set; }
        public string ExcelArchiveDatabaseConnectionString { get; set; }    
        public Versiondata[] VersionData { get; set; }
        public string OutputXbrlFolder { get; set; }
        public Loggerfiles LoggerFiles { get; set; }
    }
   
    public class Versiondata
    {
        public string version { get; set; }        
        public string SystemDatabaseString { get; set; }
        public string EiopaConnectionString { get; set; }
        public string ExcelTemplateFile { get; set; }
    }
    public class Loggerfiles
    {
        public string LoggerValidatorFile { get; set; }
        public string LoggerXbrlFile { get; set; }
        public string LoggerXbrlReaderFile { get; set; }
        public string LoggerExcelWriterFile { get; set; }
        public string LoggerExcelReaderFile { get; set; }
        public string LoggerAggregatorFile { get; set; }
    }


}
