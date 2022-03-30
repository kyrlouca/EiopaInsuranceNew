using System;
using ConfigurationNs;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Validations;
using GeneralUtilsNs;
using HelperInsuranceFunctions;

namespace ValidationCall
{
    public class Program
    {

        public static int Main(string[] args)

        {
#if (DEBUG)
            if (1 == 1)
            {

                var solvencyVer = "IU260";
                var configObject = Configuration.GetInstance(solvencyVer).Data;
                using var connectionPension = new SqlConnection(configObject.LocalDatabaseConnectionString);
                var sqlLatestDoc = "select top 1 doc.InstanceId, PensionFundId from DocInstance doc order by doc.InstanceId desc";

                (var docId, var fundIdDg) = connectionPension.QuerySingleOrDefault<(int, int)>(sqlLatestDoc, new { });

                //var validator = new DocumentValidator(fundId, docId, 0);  //the third argument to test a specific rule
                //1005 empty
                //4382 simple with filter
                //6656 val several  but same
                //6760 match
                //6442 emtpy
                //6611 empty if then
                //6702  no scope and  row col
                //6707 no scope and row
                //6729 sum with row and col
                //6699 sum and filter with two terms
                //6768 to check ftdv (in filter)
                //6933 fallback


                //var validatorDg = new DocumentValidator(solvencyVer, docIdDg);




                //var validatorDg = new DocumentValidator(solvencyVer, docId, 3141);//min(max
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 849 , 862); //sum without snn
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 929); /sum with snn
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 1005); //simple empty
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 4382); //simple filter
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 4392); //ftdv                
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 4925); //nilled
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 4627); //ExDimVal
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 4282); //ExDimVal with value
                //3147
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 4397);//open table without sum
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 4934); //open table refers to single
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 994);// sum of a closed table
                //var validatorDg = new DocumentValidator(solvencyVer, docId, 1355); // ==x0
                
                docId = 8685; //uinversal//4822, 4427,4407
                docId = 8694;//hellenic alico                
                docId = 8689;//universal error 5232,2572

                
                
                
                
                docId = 9713;//HD
                docId = 9715;//Eurolife                
                docId = 9721;//Commercial          ,5407      
                docId = 9722;//GIC
                
                docId = 9712;//hehllenic  alico              
                docId = 9724;//Hydra            
                docId = 9723;//Ethniki            
                
                docId = 9727;//868,1066

                
                docId = 9712;//hehllenic  alico   1071           
                docId = 9719;//American Hellenic 2829,3159
                var validatorDg = new DocumentValidator(solvencyVer, docId,0);//
                var x = validatorDg.ValidateDocument();

                return 1; 
            }

#else
            if (args.Length == 2)
            {                                
                //.\ValidationCaller.exe "IU260" 8691
                var solvencyVersion = args[0].Trim();                  
                var docIdx = int.TryParse(args[1], out var arg1) ? arg1 : 0;                
                var validator = new DocumentValidator(solvencyVersion, docIdx); //creates Document rules 
                //validator.CreateModuleAndDocumentRules();
                var xDg = validator.ValidateDocument();  // parses and checks each rule 
                return 1;
            }
            else
            {
                var message = @".\ValidationCaller solvencyVersion DocumentId";
                Console.WriteLine(message);
                return 0;
            }

            return 1;
#endif
        }        
    }
}
