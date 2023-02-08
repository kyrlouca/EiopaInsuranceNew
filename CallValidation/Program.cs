
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

    int docId;
    docId = 8685; //uinversal//4822, 4427,4407
    docId = 8694;//hellenic alico                
    docId = 11833;

    Console.WriteLine($"{docId}");
    //var validatorDg = new DocumentValidator       
    //DocumentValidator.ValidateDocument("IU260", 11839, 121222, 0);//

    //ConfigObject = Configuration.GetInstance(SolvencyVersion).Data;


    string licenseErrorMessage;
    if (!Z.Expressions.EvalManager.ValidateLicense(out licenseErrorMessage))
    {
        //****************check for z licencnce
        throw new Exception(licenseErrorMessage);
    }

    ValidationCaller.ValidationCaller.CallValidator("IU270", 12972);//4920 /56
    //DocumentValidator.ValidateDocument("IU260", 12905, 0, 0);//4920 /56
    return 1;
}
#endif
if (args.Length == 2)
{
    //.\ValidationCaller.exe "IU260" 8691
    var solvencyVersion = args[0].Trim();
    var docIdx = int.TryParse(args[1], out var arg1) ? arg1 : 0;    
    ValidationCaller.ValidationCaller.CallValidator(solvencyVersion, docIdx);//4920 /56
    //DocumentValidator.ValidateDocument(solvencyVersion,docIdx,0);  // parses and checks each rule 
    return 1;
}
else
{
    var message = @".\ValidationCaller solvencyVersion DocumentId";
    Console.WriteLine(message);
    return 0;
}

return 1;
