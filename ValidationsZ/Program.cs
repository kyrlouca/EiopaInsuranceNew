
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

string licenseErrorMessage;
if (!Z.Expressions.EvalManager.ValidateLicense(out licenseErrorMessage))
{
    //****************check for z licencnce
    throw new Exception(licenseErrorMessage);
}

string solvency = "";
int documentID = 0;
var isDebug = false;


#if DEBUG
isDebug = true;
#endif

if (isDebug)
{


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

    DocumentValidator.StaticStartValidateDocument("PU270", 4929, 7339);
    //DocumentValidator.StaticStartValidateDocument("IU270", 12972,4876);    
    return 1;
}

if (args.Length == 2)
{
    //.\ValidationCaller.exe "IU270" 12972
    var solvencyVersion = args[0].Trim();
    var docIdx = int.TryParse(args[1], out var arg1) ? arg1 : 0;
    DocumentValidator.StaticStartValidateDocument(solvencyVersion, docIdx);//4920 /56        
    return 1;
}

var message = @"Incorrect Arguments,Correct Usage: .\ValidationCaller solvencyVersion DocumentId";
Console.WriteLine(message);
return 0;
