using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;


namespace ConfigurationNs
{

    public class ConfigObject
    {
        public string LocalDatabaseConnectionString { get; internal set; } //Use this One because it can be either pension or Insurance
        public string EiopaDatabaseConnectionString { get; internal set; } //Use this One because it depends on solvency version

        public string BackendDatabaseConnectionString { get; set; }
        public string ExcelArchiveDatabaseConnectionString { get; set; }


        public string OutputXbrlFolder { get; set; }

        public string ExcelTemplateFileGeneral { get; internal set; }

        public string LoggerXbrlReaderFile { get; set; }
        public string LoggerExcelWriterFile { get; set; }
        public string LoggerExcelReaderFile { get; set; }
        public string LoggerValidatorFile { get; set; }
        public string LoggerXbrlFile { get; set; }
        public string LoggerAggregatorFile { get; set; }

    }


    public class Configuration
    {
        //New version to read json
        //Create a Singleton -- Cannot use a static Class because I need to pass solvency version as a  parameter in the constructor 
        //use a *static* method GetInstance in a a normal class
        //The *static* method GetInstance creates an instance of the class(if first time) and returns a Singleton object of the class
        //GetInstance creates the object depending on the version
        //all the attributes are under the Data object.
        //by passing a the solvency version as a param, the user can select which database version is selected as LocalDatabaseConnection

        public static Configuration Instance { get; private set; }
        public ConfigObject Data { get; private set; }=new ConfigObject();
        public string Version { get; }
        public string Filename { get; private set; }
        public static bool IsValidVersion(string version)
        {

            var validValues = new List<string>() { "PP250", "PU250", "IU250", "IU260", "IU270", "TEST250" };
            var isValid = validValues.Contains(version);
            return isValid;
        }
        private Configuration(string version)
        {
            Version = version;


#if DEBUG
            Console.WriteLine("Mode=Debug from Config");
            Filename = @"C:\Users\kyrlo\soft\dotnet\insurance-project\EiopaInsurance\ConfigDataNewVersion.json";
#else            
            Console.WriteLine("Mode=Release from Config");
            try
            {
                Filename = Path.Combine(Directory.GetCurrentDirectory(), "ConfigDataNew.json");                               
                Console.WriteLine($"Config: {Filename}");
            }
            catch (Exception e)
            {
                var message = $"Error reading ConfigData.json from current directory ";
                Console.WriteLine(message);
                Console.WriteLine(e);
                throw new Exception(message);
            }
#endif

            var jsonDataString = "";
            try
            {
                jsonDataString = File.ReadAllText(Filename);
            }
            catch (Exception e)
            {
                var message = $"Error Reading file:{Filename}-exception {e.Message} ";
                Console.WriteLine(message);
                throw new Exception( message);
            }

            JsonDataClass jsonData;
            try
            {
                jsonData = JsonSerializer.Deserialize<JsonDataClass>(jsonDataString);
            }
            catch (Exception e)
            {
                var justFileName = Path.GetFileName(Filename);
                var message = $" Cannot read Json file:{justFileName} /n--{e.Message} ";
                Console.WriteLine(message);
                throw new Exception(message);
            }


            var versionData = jsonData.VersionData.FirstOrDefault(item => item.version == version);
            if(versionData is null)
            {
                var message = $"Cannot find item for version : {version} ";
                Console.WriteLine(message);
                throw new Exception(message);
            }

            Data.BackendDatabaseConnectionString = jsonData.BackendDatabaseConnectionString;
            Data.ExcelArchiveDatabaseConnectionString = jsonData.ExcelArchiveDatabaseConnectionString;
            Data.OutputXbrlFolder = jsonData.OutputXbrlFolder;
            Data.LocalDatabaseConnectionString = versionData.SystemDatabaseString;
            Data.EiopaDatabaseConnectionString = versionData.EiopaConnectionString;
            Data.ExcelTemplateFileGeneral = versionData.ExcelTemplateFile;

            Data.LoggerXbrlFile = jsonData.LoggerFiles.LoggerXbrlFile;
            Data.LoggerXbrlReaderFile = jsonData.LoggerFiles.LoggerXbrlReaderFile;
            Data.LoggerValidatorFile = jsonData.LoggerFiles.LoggerValidatorFile;
            Data.LoggerExcelReaderFile = jsonData.LoggerFiles.LoggerExcelReaderFile;
            Data.LoggerExcelWriterFile = jsonData.LoggerFiles.LoggerExcelWriterFile;
            Data.LoggerAggregatorFile = jsonData.LoggerFiles.LoggerAggregatorFile;

        }
        public static Configuration GetInstance(string version)
        {
            Instance ??= new Configuration(version);
            return Instance;
        }
    }

}
