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
        public static bool ToDeleteCallValidator(string solvencyVersion,int documentId)
        {

            var  configObjectNew = HostCreator.CreateTheHost(solvencyVersion);

            //var configObject = GetConfiguration(solvencyVersion);
            //DocumentValidator.ValidateDocument(configObjectNew, documentId,0);  // parses and checks each rule 

            return true;
        }

    }

}
