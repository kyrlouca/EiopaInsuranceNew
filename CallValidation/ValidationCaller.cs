using System;
using System.Collections.Generic;
using ConfigurationNs;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Validations;
using Shared.Services;

namespace ValidationCaller
{
    public class ValidationCaller
    {
        //(ConfigObject configObject, int documentId, int testingRuleId = 0, int testingTechnicalRuleId = 2)
        public static bool CallValidator(string solvencyVersion,int documentId)
        {

            var  configObjectNew = HostCreator.CreateTheHost(solvencyVersion);

            //var configObject = GetConfiguration(solvencyVersion);
            DocumentValidator.ValidateDocument(configObjectNew, documentId,0);  // parses and checks each rule 
            return true;
        }

        private static void GetConfiguration(string solvencyVersion)
        {
            
            if (!Configuration.IsValidVersion(solvencyVersion))
            {
                var errorMessage = $"Excel Writer --Invalid Eiopa Version: {solvencyVersion}";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);                
            }


             var configObject = Configuration.GetInstance(solvencyVersion).Data;

            if (string.IsNullOrEmpty(configObject.LoggerValidatorFile))
            {
                var errorMessage = "LoggerValidatorFile is not defined in ConfigData.json";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }
                       

            //the connection strings depend on the Solvency Version
            if (string.IsNullOrEmpty(configObject.EiopaDatabaseConnectionString) || string.IsNullOrEmpty(configObject.LocalDatabaseConnectionString))
            {
                var errorMessage = "Empty ConnectionStrings in ConfigData.json file";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }
            //return configObject;
            
        }


    }

}
