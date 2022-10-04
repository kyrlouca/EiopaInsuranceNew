
using System;
using ConfigurationNs;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using GeneralUtilsNs;
//using HelperInsuranceFunctions;
using Validations;

#if (DEBUG)
            if (1 == 1)
            {

                //var solvencyVer = "IU260";
                //var configObject = Configuration.GetInstance(solvencyVer).Data;
                //using var connectionPension = new SqlConnection(configObject.LocalDatabaseConnectionString);
                //var sqlLatestDoc = "select top 1 doc.InstanceId, PensionFundId from DocInstance doc order by doc.InstanceId desc";

                //(var docId, var fundIdDg) = connectionPension.QuerySingleOrDefault<(int, int)>(sqlLatestDoc, new { });

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


                int docId;
                docId = 8685; //uinversal//4822, 4427,4407
                docId = 8694;//hellenic alico                
                docId = 8689;//universal error 5232,2572

                docId = 9719;//American Hellenic 2829,3159
                
               docId = 9712;//hehllenic  alico   1071           

                docId = 9723;//Ethniki            
                docId = 9722;//GIC
                docId = 9721;//Commercial          ,5407
                docId = 9715;//Eurolife                
                docId = 9727;//Hydra 868,1066dd
                docId = 9732;// 2814 ,3256
                docId = 9734;//  4745 ,794, 994 ,4798d
                docId = 9741;//  5261
                docId = 9770;//797,4669 ,4782,1405,30158
                docId = 9786;//4629
                             //9794,5376 empty match
                             //9798,5265  by maria ethniki insurance
                docId = 11811;//to check rule 5236 for null values
                docId = 11812;//to check for small differences
                docId = 11823;//to check for small differences
                docId = 11833;

                Console.WriteLine($"{docId}");
    //var validatorDg = new DocumentValidator   
    //DocumentValidator.ValidateDocument("IU260", 9800, 4342);
    //DocumentValidator.ValidateDocument("IU260", 10820, 0);                

    //DocumentValidator.ValidateDocument("IU260", 11833, 6406);
    //DocumentValidator.ValidateDocument("IU260", 11839, 121222, 0);//

    string licenseErrorMessage;
    if (!Z.Expressions.EvalManager.ValidateLicense(out licenseErrorMessage))
    {
        throw new Exception(licenseErrorMessage);
    }


    DocumentValidator.ValidateDocument("IU260", 12905,0, 0);//4920 /56

                return 1;
            }

#endif
            if (args.Length == 2)
            {                                
                //.\ValidationCaller.exe "IU260" 8691
                var solvencyVersion = args[0].Trim();                  
                var docIdx = int.TryParse(args[1], out var arg1) ? arg1 : 0;                
                //var validator = new DocumentValidator(solvencyVersion, docIdx); //creates Document rules 
                //validator.CreateModuleAndDocumentRules();
                DocumentValidator.ValidateDocument(solvencyVersion,docIdx,0);  // parses and checks each rule 
                return 1;
            }
            else
            {
                var message = @".\ValidationCaller solvencyVersion DocumentId";
                Console.WriteLine(message);
                return 0;
            }

            return 1;
