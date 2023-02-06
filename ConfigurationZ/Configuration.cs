

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ConfigurationNs
{

    public class ConfigObject
    {

        //somehow I could not manage to use the internal modifier properly. It does not work for internal set from serializer
        public string LocalDatabaseConnectionString { get; internal set; } //Use this One because it can be either pension or Insurance
        public string EiopaDatabaseConnectionString { get; internal set; } //Use this One because it depends on solvency version

        public string PensionDatabaseConnectionString { internal get; set; }
        public string InsuranceDatabaseConnectionString { internal get; set; }
        public string BackendDatabaseConnectionString { get; set; }
        public string ExcelArchiveDatabaseConnectionString { get; set; }

        public string EiopaUnified250ConnectionString { internal get; set; }//do NOT use any of this directly. Use LocalDatabaseConnection
        public string EiopaUnified260ConnectionString { internal get; set; }
        public string EiopaPension250ConnectionString { internal get; set; }

        public string OutputXbrlFolder { get; set; }

        public string ExcelTemplateFileGeneral { get; internal set; }
        public string ExcelTemplateFile260 { internal get; set; }
        public string ExcelTemplateFile250 { internal get; set; }
        public string ExcelPensionFile250 { internal get; set; }

        public string LoggerXbrlReaderFile { get; set; }
        public string LoggerExcelWriterFile { get; set; }
        public string LoggerExcelReaderFile { get; set; }
        public string LoggerValidatorFile { get; set; }
        public string LoggerXbrlFile { get; set; }
        public string LoggerAggregatorFile { get; set; }
    }


    public class Configuration
    {

        //Create a Singleton -- Cannot use a static Class because I need to pass solvency version as a  parameter in the constructor 
        //use a *static* method GetInstance in a a normal class
        //The *static* method GetInstance creates an instance of the class(if first time) and returns a Singleton object of the class
        //GetInstance creates the object depending on the version
        //all the attributes are under the Data object.
        //by passing a the solvency version as a param, the user can select which database version is selected as LocalDatabaseConnection

        public static Configuration Instance { get; private set; }
        public ConfigObject Data { get; private set; }
        public string Version { get; }
        public string Filename { get; private set; }
        public static bool IsValidVersion(string version)
        {

            var validValues = new List<string>() {"PU270", "PP250", "PU250", "IU250", "IU260", "TEST250" };
            var isValid = validValues.Contains(version);
            return isValid;
        }
        private Configuration(string version)
        {
            Version = version;


#if DEBUG
            Console.WriteLine("Mode=Debug from Config");
            Filename = @"C:\Users\kyrlo\soft\dotnet\insurance-project\EiopaInsurance\ConfigData.json";
#else            
            Console.WriteLine("Mode=Release from Config");
            try
            {
                Filename = Path.Combine(Directory.GetCurrentDirectory(), "ConfigData.json");                               
                Console.WriteLine($"Config: {Filename}");
            }
            catch (Exception e)
            {
                var message = $"Error  getting ConfigData.json from current directory ";
                Console.WriteLine(message);
                Console.WriteLine(e);
                throw;
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
                throw;
            }

            try
            {
                Data = JsonSerializer.Deserialize<ConfigObject>(jsonDataString);
            }
            catch (Exception e)
            {
                var message = $"Error Deseriliazing Json in  file:{Filename} \n--{e.Message} ";
                Console.WriteLine(message);
                throw;
            }


            switch (version)
            {
                case "PP250": //Pension Database using Eiopa Pension 250
                    Data.LocalDatabaseConnectionString = Data.PensionDatabaseConnectionString;
                    Data.EiopaDatabaseConnectionString = Data.EiopaPension250ConnectionString;
                    Data.ExcelTemplateFileGeneral = Data.ExcelPensionFile250;
                    break;
                case "PU250"://Pension Database using Eiopa Unified 250
                    Data.LocalDatabaseConnectionString = Data.PensionDatabaseConnectionString;
                    Data.EiopaDatabaseConnectionString = Data.EiopaUnified250ConnectionString;
                    Data.ExcelTemplateFileGeneral = Data.ExcelPensionFile250;
                    break;
                case "IU250"://Insurance Database using Eiopa Unified 250
                    Data.LocalDatabaseConnectionString = Data.InsuranceDatabaseConnectionString;
                    Data.EiopaDatabaseConnectionString = Data.EiopaUnified250ConnectionString;
                    Data.ExcelTemplateFileGeneral = Data.ExcelTemplateFile250;
                    break;
                case "IU260"://Insurance Database using Eiopa Unified 260
                    Data.LocalDatabaseConnectionString = Data.InsuranceDatabaseConnectionString;
                    Data.EiopaDatabaseConnectionString = Data.EiopaUnified260ConnectionString;
                    Data.ExcelTemplateFileGeneral = Data.ExcelTemplateFile260;
                    break;
                case "TEST250"://Insurance Database but for PENSION EXCEL 
                    Data.LocalDatabaseConnectionString = Data.InsuranceDatabaseConnectionString;
                    Data.EiopaDatabaseConnectionString = Data.EiopaUnified250ConnectionString;
                    Data.ExcelTemplateFileGeneral = Data.ExcelPensionFile250;
                    break;
                default:
                    Data.LocalDatabaseConnectionString = "";
                    Data.EiopaDatabaseConnectionString = "";
                    Console.WriteLine("Invalid Eiopa Version");
                    break;
            }



        }
        public static Configuration GetInstance(string version)
        {
            Instance ??= new Configuration(version);
            return Instance;
        }
    }

}

