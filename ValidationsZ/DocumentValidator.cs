using System;
using System.Collections.Generic;
using System.Text;
using GeneralUtilsNs;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using EntityClasses;
using EntityClassesZ;
using System.Text.RegularExpressions;
using EiopaConstants;
using ConfigurationNs;
using Z.Expressions;
using HelperInsuranceFunctions;
using TransactionLoggerNs;



namespace Validations
{

    internal enum ValidStatus { Valid, Error, Waring };

    public class DocumentValidator
    {
        //create the structure which contains lists DocumentRules Derived from Validation Rules
        //First we create the Rules that apply to the module 
        //and then we creat the rules for the document
        //for the actual validation use ValidateDocument()        
        public int DocumentId { get; private set; }
        public int ModuleId { get; private set; }
        public DocInstance DocumentInstance { get; private set; }
        public bool IsValidDocument { get; private set; } = true;

        public string SolvencyVersion { get; private set; }

        public List<RuleStructure> ModuleRules { get; private set; } = new List<RuleStructure>();
        public List<RuleStructure> DocumentRules { get; private set; } = new List<RuleStructure>();
        public int TestingRuleId { get; set; } = 0;
        public ConfigObject ConfigObject { get; private set; }



        public DocumentValidator(string solverncyVersion, int documentId, int testingRuleId = 0)
        {

            SolvencyVersion = solverncyVersion;
            DocumentId = documentId;
            TestingRuleId = testingRuleId;


            GetConfiguration();


            var document = InsuranceData.GetDocumentById(documentId);
            if (document is null)
            {
                IsValidDocument = false;
                var messg = $"Validation: Document  NOT Found. Document Id: {DocumentId} ";
                Log.Error(messg);

                var trans = new TransactionLog()
                {
                    PensionFundId = document.PensionFundId,
                    ModuleCode = document.ModuleCode,
                    ApplicableYear = document.ApplicableYear,
                    ApplicableQuarter = document.ApplicableQuarter,
                    Message = messg,
                    UserId = 0,
                    ProgramCode = ProgramCode.VA.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = document.InstanceId,
                    MessageType = MessageType.ERROR.ToString()
                };
                TransactionLogger.LogTransaction(SolvencyVersion, trans);

                return;
            }

            var status = document.Status.Trim();
            var isLockedDocument = status == "P" || status == "S";
            if (isLockedDocument)
            {
                IsValidDocument = false;
                var messg = status == "P"
                    ? $"DocumentId: {DocumentId}. Document currently being Processed by another User"
                    : $"DocumentId: {DocumentId}. Document has already been submitted";
                Log.Error(messg);
                var trans = new TransactionLog()
                {
                    PensionFundId = document.PensionFundId,
                    ModuleCode = document.ModuleCode,
                    ApplicableYear = document.ApplicableYear,
                    ApplicableQuarter = document.ApplicableQuarter,
                    Message = messg,
                    UserId = 0,
                    ProgramCode = ProgramCode.VA.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = document.InstanceId,
                    MessageType = MessageType.ERROR.ToString()
                };
                TransactionLogger.LogTransaction(SolvencyVersion, trans);

                return;
            }

            DocumentInstance = document;
            DocumentId = document.InstanceId;

            var module = GetModuleId();

            if (module is null)
            {
                IsValidDocument = false;
                return;
            }
            ModuleId = module.ModuleID;

            var message = $"---Validation started for Document:{DocumentId}";
            Log.Information(message);

            //to prevent anyone else validating when processed
            UpdateDocumentStatus("P");

            CreateErrorDocument();

            //create the rules. First create  the  rules of the module (ars, qrs, etc ..)
            //then, for each module rule create the document rules for each table which have the same tableCode as the rule scope table code.
            CreateModuleAndDocumentRules();
        }


        private void UpdateDocumentStatus(string status)
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqlUpdate = @"update DocInstance  set status= @status where  InstanceId= @documentId;";
            var doc = connectionInsurance.Execute(sqlUpdate, new { DocumentId, status });
        }


        public bool ValidateDocument(int selecteRule = 0)
        {
            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var errorCounter = 0;
            var warningCounter = 0;
            var rulesCounter = 0;

            if (!IsValidDocument)
            {
                return false;
            }
            Console.WriteLine($"v1.000 : Validate Document doc:{DocumentId}");

            
            Console.WriteLine($"Check Fact enum values");
            var isFactValuesValid = (1 == 2) && ValidateFactEnumValues(); //validation withour rules
            //var isFactValuesValid = true;
            if (!isFactValuesValid)
            {
                //updates document as error but keeps finding errors                
            }

            Console.WriteLine($"Check Unique Keys");
            //var isKeyValuesUnique = (1 == 1) && ValidateOpenTableKeysUnique(DocumentId);
            if (HasEmptySheets(DocumentId))
            {
                //retrun
            }

            if (selecteRule > 0)
            {
                DocumentRules = DocumentRules.Where(item => item.ValidationRuleId == selecteRule).ToList();
            }

            //****************************************************************
            //Check all the document rules
            Console.WriteLine($"Rule Validation");
            foreach (var rule in DocumentRules)
            {
                Console.WriteLine($"ruleId:{rule.ValidationRuleId}");
                //Console.Write($"{rule.ValidationRuleId}");
                if (rule.RuleTerms.Any(term => term.DataTypeOfTerm == DataTypeMajorUU.UnknownDtm && !term.IsFunctionTerm))
                {
                    //when creating Document rules from Module rules we used the scope 
                    //the scope was expanded by adding on the range and some   rules for non-existent rows/columns were created
                    //the terms for these rules have an unknown datatype
                    continue;
                }
                rulesCounter = +1;

                var ruleTables = rule.RuleTerms
                        .Where(term => !term.IsFunctionTerm)
                        .Select(term => term.TableCode)
                        .Distinct().ToList();

                var isRuleIgnore = ruleTables.Any(tableCode => !IsTableInDocument(tableCode));
                if (isRuleIgnore)
                {
                    //if the rule contains a sheet which is not in the document then ignore the rule
                    continue;
                }

                var isRuleValid = rule.ValidateTheRule();

                if (!isRuleValid)
                {
                    var isError = rule.ValidationRuleDb.Severity == "Error";
                    var isWarning = rule.ValidationRuleDb.Severity == "Warning";
                    errorCounter = isError ? errorCounter + 1 : errorCounter;
                    warningCounter = isWarning ? warningCounter + 1 : warningCounter;

                    var errorTerms = rule.RuleTerms.Select(term => term.TextValue).ToArray();
                    var errorValue = string.Join("---", errorTerms);


                    var errorRule = new ERROR_Rule
                    {
                        RuleId = rule.ValidationRuleId,
                        ErrorDocumentId = DocumentId,
                        Scope = GeneralUtils.TruncateString(rule.ScopeString, 800),
                        TableBaseFormula = GeneralUtils.TruncateString(rule.TableBaseFormula, 990),
                        Filter = GeneralUtils.TruncateString(rule.FilterFormula, 990),
                        SheetId = 0,
                        SheetCode = rule.ScopeTableCode,
                        RowCol = rule.ScopeRowCol,
                        RuleMessage = GeneralUtils.TruncateString(rule.ValidationRuleDb.ErrorMessage, 2490),
                        IsWarning = isWarning,
                        IsError = isError,
                        IsDataError = false,
                        Row = "",
                        Col = "",
                        DataValue = GeneralUtils.TruncateString(errorValue, 490),
                        DataType = ""

                    };

                    CreateRuleError(errorRule);
                    //Log.Error("Invalid Rule");
                }

            }
            Log.Information($"Number of Validation Rules:{rulesCounter}");
            Log.Information($"Number of Validation ERRORS: {errorCounter}, Warnings:{warningCounter}");


            var sqlCountErrors = @"
                select 
                    sum(case when er.IsError=1 then 1 else 0 end) as sErr,
                    sum(case when er.IsWarning=1 then 1 else 0 end) as wErr,
                    sum(case when er.IsDataError=1 then 1 else 0 end) as dErr
                    from ERROR_Rule er    
                  where er.ErrorDocumentId=@documentId
                ";

            (var severeErrors, var warningErrors, var dataErrors) = connectionPension.QuerySingleOrDefault<(int, int, int)>(sqlCountErrors, new { DocumentId });
            var totalErrors = severeErrors + dataErrors;
            var isDocumentValid = totalErrors == 0;

            var sqlUpdate = @"update ERROR_Document set IsDocumentValid=@isDocumentValid, errorCounter=@eCounter, WarningCounter=@wCounter where ErrorDocumentId=@documentId";
            connectionPension.Execute(sqlUpdate, new { isDocumentValid, eCounter = totalErrors > 0, wCounter = warningErrors > 0, DocumentId });

            var status = (totalErrors == 0) ? "V" : "E";
            status = DocumentRules.Count > 0 ? status : "E";
            UpdateDocumentStatus(status);


            return isDocumentValid;
        }


        private void CreateModuleAndDocumentRules()
        {

            ///****** Read the Module Rules and Construct the Document Rules             
            //Document rules have both row and col (based on scope) and their RuleTerms and FilterTerms are evaluated
            //--A ruleTerm has plain terms and function terms. {S.26.01.01.02, r0210,c0060} ,  max(0, {S.26.01.01.01, r0210,c0020})
            //--First, evaluate the plain terms and then the function terms using the plain terms
            //--Function terms may be nested (min(max...)

            Console.WriteLine($"Starting validating {DocumentId}");
            Console.WriteLine($"Create Module Rules");

            //Module Rules
            CreateModuleRules();

            //DocumentRules
            Console.WriteLine("\nCreate Document Rules");
            CreateDocumentRulesFromModuleRules();

            Console.WriteLine("\n update rule terms");
            foreach (var rule in DocumentRules)
            {
                //Console.WriteLine(".");
                Console.Write($"\nupdate rule terms for rule:{rule.ValidationRuleId}");
                UpdateRuleAndFilterTerms(rule);
            }

            return;
        }

        private void UpdateRuleAndFilterTerms(RuleStructure rule)
        {
            //Console.Write($"");

            //*****RuleTerms
            var plainTerms = rule.RuleTerms.Where(term => !term.IsFunctionTerm).ToList(); // {S.06.02.01.01,c0170,snnn} for terms like these we cannot get a  direct db value
            plainTerms.ForEach(term => UpdatePlainTerm(rule, term));

            //***FOR NESTED functions only: evaluate function T Terms ** T terms exist only for nested functions
            //"T" terms  are the inner nested terms and  should be evaluated first T = max(Z1)
            var functionTerms = rule.RuleTerms.Where(term => term.IsFunctionTerm && term.Letter.Contains("T")).ToList();
            functionTerms.ForEach(term => UpdateSingleFunctionTerm(rule, rule.RuleTerms, term, rule.FilterFormula));

            //evaluate function Z Terms
            //"Z" terms are the function terms (without nesting) using plain terms as parameters Z = min(X1)            
            var functionZetTerms = rule.RuleTerms.Where(term => term.IsFunctionTerm && term.Letter.Contains("Z")).ToList();
            functionZetTerms.ForEach(term => UpdateSingleFunctionTerm(rule, rule.RuleTerms, term, rule.FilterFormula));
           


            //*******Filter terms
            //filter terms for rules containing sum(snnn are used to filter out rows for the sum 
            if (!(string.IsNullOrWhiteSpace(rule.FilterFormula) || rule.TableBaseFormula.Contains("SNNN")))
            {
                //plain terms
                var plainFilterTerms = rule.FilterTerms.Where(term => !term.IsFunctionTerm).ToList();
                plainFilterTerms.ForEach(term => UpdatePlainTerm(rule, term));

                //evaluate function T Terms
                var functionFilterTerms = rule.FilterTerms.Where(term => term.IsFunctionTerm && term.Letter.Contains("T")).ToList();
                functionFilterTerms.ForEach(term => UpdateSingleFunctionTerm(rule, rule.FilterTerms, term, rule.FilterFormula));

                //evaluate function Z Terms
                var functionFilterZetTerms = rule.FilterTerms.Where(term => term.IsFunctionTerm && term.Letter.Contains("Z")).ToList();
                functionFilterZetTerms.ForEach(term => UpdateSingleFunctionTerm(rule, rule.FilterTerms, term, rule.FilterFormula));
                

            }

        }


        private void UpdateSingleFunctionTerm(RuleStructure rule, List<RuleTerm> allTerms, RuleTerm term, string filterFomula)
        {


            var termLetterx = RegexValidationFunctions.FunctionTypesRegex.Match(term.TermText).Groups[2]?.Value ?? "";
            switch (term.FunctionType)
            {
                case FunctionTypes.NILLED:

                    term.DataTypeOfTerm = DataTypeMajorUU.BooleanDtm;

                    var termLetterNilled = RegexValidationFunctions.FunctionTypesRegex.Match(term.TermText).Groups[2]?.Value ?? "";
                    var termValueNilled = allTerms.FirstOrDefault(term => term.Letter == termLetterNilled);
                    term.IsMissing = false; //the term isMissing should always be false since we testing for missing terms
                    term.BooleanValue = termValueNilled.IsMissing || string.IsNullOrWhiteSpace(termValueNilled.TextValue);
                    break;
                case FunctionTypes.EMPTY:

                    term.DataTypeOfTerm = DataTypeMajorUU.BooleanDtm;

                    var termLetter = RegexValidationFunctions.FunctionTypesRegex.Match(term.TermText).Groups[2]?.Value ?? "";
                    var termValue = allTerms.FirstOrDefault(term => term.Letter == termLetter);
                    term.IsMissing = false; //the term cannot be missing since we testing for missing terms
                    term.BooleanValue = termValue.IsMissing || string.IsNullOrWhiteSpace(termValue.TextValue);
                    break;
                case FunctionTypes.ISFALLBACK:
                    //TermText = "isfallback(X0)"=> get the value of X0 from Ruleterms list                      
                    term.DataTypeOfTerm = DataTypeMajorUU.BooleanDtm;

                    var termLetterFB = RegexValidationFunctions.FunctionTypesRegex.Match(term.TermText).Groups[2]?.Value ?? "";
                    termValue = allTerms.FirstOrDefault(term => term.Letter == termLetterFB);
                    term.IsMissing = false; //the result of the function will be true if the term is missing
                    term.BooleanValue = termValue.IsMissing || string.IsNullOrWhiteSpace(termValue.TextValue);
                    break;
                case FunctionTypes.MIN:
                    //TermText = min(2,X1+3,X2)                                        
                    var allTermsDict = allTerms.ToDictionary(term => term.Letter, term => (double)(term.DecimalValue));
                    var minTermsStr = GeneralUtils.GetRegexSingleMatch(@"min\((.*)\)", term.TermText).Split(",");

                    var minValArray = minTermsStr.Select(term => Eval.Execute<double>(term, allTermsDict));
                    var minVal = minValArray.Min();

                    term.DataTypeOfTerm = DataTypeMajorUU.NumericDtm;
                    term.IsMissing = false;
                    term.DecimalValue = Convert.ToDecimal(minVal);
                    break;
                case FunctionTypes.MAX:
                    //TermText = max(xx,X1,X2)
                    var allTermsDictM = allTerms.ToDictionary(term => term.Letter, term => (double)(term.DecimalValue));                    
                    var maxTermsStr = GeneralUtils.GetRegexSingleMatch(@"max\((.*)\)", term.TermText).Split(",");

                    var maxValArray = maxTermsStr.Select(term => Eval.Execute<double>(term, allTermsDictM));
                    var maxVal = maxValArray.Max();

                    term.DataTypeOfTerm = DataTypeMajorUU.NumericDtm;
                    term.IsMissing = false;
                    term.DecimalValue = Convert.ToDecimal(maxVal);
                    break;
                case FunctionTypes.MATCHES:
                    //"matches(X0,\"^..((71)|(75)|(8.)|(95))$\")"=> "^..((71)|(75)|(8.)|(95))$"
                    //matches(ftdv({S.06.02.01.02,c0290},"s2c_dim:UI"),"^CAU/(ISIN/.*)|(INDEX/.*)"))	

                    var test = RegexValidationFunctions.FunctionTypesRegex.Match(term.TermText);
                    var termText = RegexValidationFunctions.FunctionTypesRegex.Match(term.TermText).Groups[2].Value;

                    var splitReg = @"(.+),""(.+)""";
                    var termParts = GeneralUtils.GetRegexSingleMatchManyGroups(splitReg, termText);
                    if (termParts.Count != 3)
                    {
                        term.BooleanValue = true;
                        break;
                    }
                    var pattern = termParts[2];
                    pattern = pattern.Replace(@"/", @"\/"); //^CAU/(ISIN/.*)=>"^CAU\/(ISIN\/.*) 

                    var termLetterM = termParts[1];
                    var valueTerm = allTerms.FirstOrDefault(term => term.Letter == termLetterM);                    
                    var val = valueTerm.TextValue.Trim();
                    term.IsMissing = valueTerm.IsMissing;
                    term.DataTypeOfTerm = DataTypeMajorUU.BooleanDtm;
                    term.BooleanValue = Regex.IsMatch(valueTerm.TextValue, pattern);
                    break;
                case FunctionTypes.SUM:
                    var termLetterS = RegexValidationFunctions.FunctionTypesRegex.Match(term.TermText).Groups[2]?.Value ?? "";
                    var sumTerm = allTerms.FirstOrDefault(term => term.Letter == termLetterS);
                    var isOpenTableSum = IsOpenTable(ConfigObject, sumTerm.TableCode);
                    if (!isOpenTableSum || !sumTerm.TermText.ToUpper().Contains("SNNN"))
                    {
                        //sumTerm.SheetId = rule.SheetId;
                        term.DataTypeOfTerm = DataTypeMajorUU.NumericDtm;
                        term.DecimalValue = FunctionForSumTermForCloseTableNew(rule, sumTerm);
                    }
                    else
                    {

                        term.DataTypeOfTerm = DataTypeMajorUU.NumericDtm;
                        term.DecimalValue = FunctionForOpenSumNew(sumTerm, filterFomula);

                    };

                    break;
                case FunctionTypes.FTDV:
                    term.DataTypeOfTerm = DataTypeMajorUU.StringDtm;
                    term.IsMissing = false;
                    term.TextValue = FunctionForFtdvValue(allTerms, term);
                    break;
                case FunctionTypes.EXDIMVAL:
                    term.DataTypeOfTerm = DataTypeMajorUU.StringDtm;
                    term.IsMissing = false;
                    term.TextValue = FunctionForExDimVal(allTerms, term);
                    break;
                case FunctionTypes.EXP:
                    term.DataTypeOfTerm = DataTypeMajorUU.NumericDtm;
                    term.IsMissing = false;
                    term.DecimalValue = Convert.ToDecimal(FunctionForExp(allTerms, term));
                    break;
                case FunctionTypes.LIKE:
                    term.DataTypeOfTerm = DataTypeMajorUU.BooleanDtm;
                    term.IsMissing = false;
                    term.BooleanValue = false;
                    break;
                default:

                    Console.WriteLine("");
                    break;
            }
            return;
        }

        private int UpdatePlainTerm(RuleStructure rule, RuleTerm plainTerm)
        {
            //var dbValue = EvaluateTermFunction(term);
            if (plainTerm.IsSum)
            {
                //sum terms for either closed or open tables will be evaluated later as functions
                //make it numeric to avoid rejection of rule
                plainTerm.SheetId = rule.SheetId;
                plainTerm.DataTypeOfTerm = DataTypeMajorUU.NumericDtm;
                return 0;
            }
            var dbValue = plainTerm.TableCode == rule.ScopeTableCode
                ? GetCellValueFromOneSheetDb(ConfigObject, plainTerm.TableCode, rule.SheetId, plainTerm.Row, plainTerm.Col)
                : GetCellValueFromDbNew(ConfigObject, DocumentId, plainTerm.TableCode, plainTerm.Row, plainTerm.Col);
            plainTerm.AssignDbValues(dbValue);
            return 0;
        }


        public static DbValue GetCellValueFromOneSheetDb(ConfigObject configObj, string tableCode, int sheetId, string row, string col)
        {
            using var connectionPension = new SqlConnection(configObj.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(configObj.EiopaDatabaseConnectionString);
            //We may have two sheets with the same sheetCode in one Document due to Z dim
            //Therefore, we must use the SheetId and not just the sheetCode for these facts. (because of sheets with the same sheetcode)
            //On the other hand, if a ruleTerm refers to a fact in another sheet, we have to use the sheet Code and NOT the sheetID


            var sqlFact = @"
                SELECT
                  fact.TemplateSheetId
                 ,fact.FactId
                 ,fact.Row
                 ,fact.Col
                 ,fact.Zet
                 ,fact.TextValue
                 ,fact.NumericValue
                 ,fact.Decimals
                 ,fact.DateTimeValue
                 ,fact.DataType
                 ,fact.DataTypeUse
                FROM TemplateSheetFact fact
                WHERE fact.TemplateSheetId = @sheetId
                AND fact.row = @row
                AND fact.Col = @col
                                ";
            var facts = connectionPension.Query<TemplateSheetFact>(sqlFact, new { sheetId, row, col });
            TemplateSheetFact fact;

            if (!facts.Any())
            {
                //it is possible that we have null facts in a sheet.
                //we need the data type 
                var sqlMapping = @"
                    select top 1 map.DATA_TYPE 
                      from MAPPING map 
                      left join mTable tab on tab.TableID=map.TABLE_VERSION_ID
                      where 
	                    tab.TableCode=@tableCode 
	                    and  DYN_TAB_COLUMN_NAME = @rowCol
	                    and map.IS_IN_TABLE=1
                    ";


                var rowCol = IsOpenTable(configObj, tableCode) ? $"{col}" : $"{row}{col}";
                var dataType = connectionEiopa.QuerySingleOrDefault<string>(sqlMapping, new { tableCode, rowCol }) ?? "";
                var majorType = CntConstants.GetMajorDataType(dataType);
                var emptyRes = new DbValue(0, "", 0, 0, new DateTime(2000, 1, 1), false, majorType, true);
                return emptyRes;
            }
            else if (facts.Count() == 1)
            {
                fact = facts.First();
                var majorDataType = CntConstants.GetMajorDataType(fact.DataTypeUse.Trim());

                var resVal = new DbValue(fact.FactId, fact.TextValue, fact.NumericValue, fact.Decimals, fact.DateTimeValue, fact.BooleanValue, majorDataType, false);
                return resVal;

            }
            else
            {

                //check for zet (same fact for row,col but  with different zet mainly for currencies and countries
                if (facts.All(fact => !string.IsNullOrWhiteSpace(fact.Zet) && fact.DataTypeUse == "M"))
                {
                    var firstFact = facts.First();
                    var majorDataType = CntConstants.GetMajorDataType(firstFact.DataTypeUse.Trim());
                    var sum = facts.Aggregate(decimal.Zero, (currentVal, item) => currentVal += (decimal)item.NumericValue);
                    var resVal = new DbValue(firstFact.FactId, firstFact.TextValue, sum, firstFact.Decimals, firstFact.DateTimeValue, firstFact.BooleanValue, majorDataType, false);
                    return resVal;
                }
            }

            var emptyRes2 = new DbValue(0, "", 0, 0, new DateTime(2000, 1, 1), false, DataTypeMajorUU.UnknownDtm, true);
            return emptyRes2;


        }

        public static DbValue GetCellValueFromDbNew(ConfigObject configObj, int docId, string tableCode, string row, string col)
        {
            using var connectionPension = new SqlConnection(configObj.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(configObj.EiopaDatabaseConnectionString);
            //We may have two sheets with the same sheetCode in one Document due to Z dim
            //Therefore, we must use the SheetId and not just the sheetCode for these facts. (because of sheets with the same sheetcode)
            //On the other hand, if a ruleTerm refers to a fact in another sheet, we have to use the sheet Code and NOT the sheetID



            var sqlFact = @"
                SELECT fact.TemplateSheetId, fact.FactId, fact.Row, fact.Col, fact.TextValue, fact.NumericValue, fact.Decimals, fact.DateTimeValue, fact.DataType,fact.DataTypeUse
                FROM TemplateSheetFact fact
                LEFT OUTER JOIN TemplateSheetInstance sheet
	                ON sheet.TemplateSheetId = fact.TemplateSheetId
                WHERE sheet.InstanceId = @DocId
	                AND sheet.TableCode = @tableCode
	                AND fact.row = @row
	                AND fact.Col = @col
                ";
            var facts = connectionPension.Query<TemplateSheetFact>(sqlFact, new { docId, tableCode, row, col });
            //var fact = connectionPension.QuerySingleOrDefault<TemplateSheetFact>(sqlFact, new { docId, tableCode, row, col });



            if (!facts.Any())
            {
                //it is possible that we have null facts in a sheet.
                //we need the data type 
                var sqlMapping = @"
                    select top 1 map.DATA_TYPE 
                      from MAPPING map 
                      left join mTable tab on tab.TableID=map.TABLE_VERSION_ID
                      where 
	                    tab.TableCode=@tableCode 
	                    and  DYN_TAB_COLUMN_NAME = @rowCol
	                    and map.IS_IN_TABLE=1
                    ";

                var rowCol = IsOpenTable(configObj, tableCode) ? $"{col}" : $"{row}{col}";
                var dataType = connectionEiopa.QuerySingleOrDefault<string>(sqlMapping, new { tableCode, rowCol }) ?? "";
                var majorType = CntConstants.GetMajorDataType(dataType);
                var emptyRes = new DbValue(0, "", 0, 0, new DateTime(2000, 1, 1), false, majorType, true);
                return emptyRes;
            }
            else if (facts.Count() == 1)
            {
                var fact = facts.First();
                var majorDataType1 = CntConstants.GetMajorDataType(fact.DataTypeUse.Trim());

                var resVal1 = new DbValue(fact.FactId, fact.TextValue, fact.NumericValue, fact.Decimals, fact.DateTimeValue, fact.BooleanValue, majorDataType1, false);
                return resVal1;

            }
            else
            {
                //check for zet (same fact for row,col but  with different zet mainly for currencies and countries
                if (facts.All(fact => !string.IsNullOrWhiteSpace(fact.Zet) && fact.DataTypeUse == "M"))
                {
                    var firstFact = facts.First();
                    var majorDataType2 = CntConstants.GetMajorDataType(firstFact.DataTypeUse.Trim());
                    var sum = facts.Aggregate(decimal.Zero, (currentVal, item) => currentVal += (decimal)item.NumericValue);
                    var resValMany = new DbValue(firstFact.FactId, firstFact.TextValue, sum, firstFact.Decimals, firstFact.DateTimeValue, firstFact.BooleanValue, majorDataType2, false);
                    return resValMany;
                }
            }

            //888888888888888

            var emptyRes2 = new DbValue(0, "", 0, 0, new DateTime(2000, 1, 1), false, DataTypeMajorUU.UnknownDtm, true);
            return emptyRes2;
        }


        private int CreateModuleRules()
        {
            //** Read the validation Rules from the Database and construct Module Rules for the corresponding Module            
            //go through each  ModuleRule and create DocumentRules with values from the document (sheets, facts)
            //THEN For each Module Rule, one or more Document Rules will be created depending on scope and filter

            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            //validationScope will provide tableId


            var sqlSelectModuleRules = @"
		  SELECT 
		         vr.ValidationRuleID
	            ,vr.ExpressionID
	            ,vr.ValidationCode
	            ,vr.Severity
	            ,vr.Scope
	            ,ex.TableBasedFormula
	            ,ex.Filter
	            ,ex.LogicalExpression
                ,ex.ErrorMessage
            FROM 
		        vValidationRuleSet vrs
                join vValidationRule vr on vr.ValidationRuleID= vrs.ValidationRuleID
                JOIN vExpression ex ON ex.ExpressionID = vr.ExpressionID
            WHERE 1=1
				and(
				(ValidationCode  like 'BV%' AND (  ex.ExpressionType <> 'NotImplementedInKYR' OR ex.ExpressionType is null) )
				OR (ValidationCode  like 'TV%'  AND ex.ExpressionType <> 'NotImplementedInKYR')   				
				)                
	            and vrs.ModuleID = @ModuleId
            ORDER BY vr.ValidationRuleID		
            ";

            var moduleValidationRules = connectionEiopa.Query<C_ValidationRuleExpression>(sqlSelectModuleRules, new { ModuleId });
            var validationRules = moduleValidationRules;

            //For TESTING  to LIMIT RULES
            if (TestingRuleId > 0)
            {
                validationRules = validationRules.Where(item => item.ValidationRuleID == TestingRuleId).ToList();
            }

            //** construct and save the validation rules for the Module
            foreach (var validationRule in validationRules)
            {
                var ruleStructure = new RuleStructure(validationRule);
                Console.Write(".");
                ModuleRules.Add(ruleStructure);
            }

            return ModuleRules.Count;
        }

        private void CreateDocumentRulesFromModuleRules()
        {
            //expand module rules using scope  for the DOCUMENT (create documentRules)

            foreach (var moduleRule in ModuleRules)
            {
                CreateDocumentRulesFromOneModuleRule(moduleRule);
            }

        }

        private void CreateDocumentRulesFromOneModuleRule(RuleStructure rule)
        {
            //For each Module rule use the SCOPE to create one or more Document Rules
            //Open tables cannot have the rows in the SCOPE, so we need to add all the ROWS of a sheet
            //Scope for closed tables may have explicit columns, range of columns or no columns
            //Scope of Closed tables  with no columns=> need to check the first term

            //var connectionPensionString = Configuration.GetConnectionPensionString();
            using var connectionEiopa = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var scopeDetails = ScopeDetails.Parse(rule.ScopeString);

            //find the actual sheet from the sheetcode of the scope. Required for open tables to go through all of its rows            
            var sqlSelectSheets = @"select sheet.TemplateSheetId, sheet.SheetCode ,sheet.IsOpenTable from TemplateSheetInstance sheet where sheet.InstanceId= @documentId and sheet.TableCode=@TableCode;";
            var sheetsUsingTheRule = connectionEiopa.Query<TemplateSheetInstance>(sqlSelectSheets, new { DocumentId, scopeDetails.TableCode }).ToList();
            var rowCols = new List<string>();

            Console.WriteLine($"\nrule:{rule.ValidationRuleId}");
            foreach (var sheet in sheetsUsingTheRule)
            {
                if (sheet.IsOpenTable)
                {
                    if (rule.RuleTerms.Any(term => term.IsSum))
                    {
                        rowCols = scopeDetails.ScopeRowCols;
                        if (rowCols.Count == 0)
                        {
                            //Add one fake rowCol just to copy the module rule as a document rule
                            rowCols.Add("NONE");
                        }

                    }
                    else
                    {


                        //For open tables, create one Document rule per row, do not use the scope 
                        var sqlDistinctRowsById = @"       
                        SELECT DISTINCT fact.Row
                        FROM TemplateSheetFact fact
                        JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId = fact.TemplateSheetId
                        WHERE  sheet.TemplateSheetId = @sheetId and sheet.InstanceId=@documentId;
                        ";

                        var rows = connectionEiopa.Query<string>(sqlDistinctRowsById, new { DocumentId, sheetId = sheet.TemplateSheetId }).ToList();
                        rowCols = rows;
                    }
                    //Console.Write("s");
                }
                else
                {
                    //depending on the scope
                    //S.22.01.01.01 (r0100;0110)
                    //S.22.01.01.01 (r0010-0090)
                    //S.22.04.01.01 (c0010)
                    //PF.02.01.24.01 closed table with no row or columns, 

                    //** for closed tables get the row cols from scope 
                    //** actually there is no need because the terms have both row and column and we can just copy the Module rule                   
                    rowCols = scopeDetails.ScopeRowCols;
                    if (rowCols.Count == 0)
                    {
                        //Add one fake rowCol just to copy the module rule as a document rule
                        rowCols.Add("NONE");
                    }

                }
                Console.Write("s");
                //Create a new rule for each rowCol (can be either a row or col. for open tables one rule for each row unless they have a sum term)
                //the axis is taken from the scope unless it is an open table which is row 
                foreach (var rowCol in rowCols)
                {
                    CreateOneDocumentRule(rule, scopeDetails, sheet, rowCol);
                }

            }
        }

        private void CreateOneDocumentRule(RuleStructure rule, ScopeDetails scopeDetails, TemplateSheetInstance sheet, string rowCol)
        {
            var newRule = rule.Clone();
            newRule.ConfigObject = ConfigObject;
            newRule.DocumentId = DocumentId;
            newRule.SheetId = sheet.TemplateSheetId;

            newRule.ScopeRowCol = rowCol;
            newRule.ScopeTableCode = scopeDetails.TableCode;

            var isSum = rule.RuleTerms.Any(term => term.IsSum);
            var scopeAxis = sheet.IsOpenTable && !isSum ? ScopeRangeAxis.Rows : scopeDetails.ScopeAxis;
            newRule.SetApplicableAxis(scopeAxis); //set the rule's axis

            //the updated rows or cols depending on the scope. However, for open linked tables find the foreign key
            var plainTerms = newRule.RuleTerms.Where(term => !term.IsFunctionTerm).ToList();
            plainTerms.ForEach(term => UpdateTermRowCol(term, scopeDetails.TableCode, scopeAxis, rowCol));

            var plainFilterTersm = newRule.FilterTerms.Where(term => !term.IsFunctionTerm).ToList();
            plainFilterTersm.ForEach(term => UpdateTermRowCol(term, scopeDetails.TableCode, scopeAxis, rowCol));

            DocumentRules.Add(newRule);
            Console.Write("+");
        }

        public void UpdateTermRowCol(RuleTerm term, string scopeTableCode, ScopeRangeAxis scopeAxis, string rowCol)
        {
            //PF.04.03.24.01 (r0040;0050;0060;0070;0080) 
            //if both row and col are present do not update anything
            if (!string.IsNullOrEmpty(term.Row) && !string.IsNullOrEmpty(term.Col))
            {
                return;
            }

            if (scopeAxis == ScopeRangeAxis.None)
            {
                //a closed table term without scope rows/cols  OR a term in a sum(snn function) OR filter terms when there is an sum(snn function)
            }
            else if (scopeAxis == ScopeRangeAxis.Cols)
            {
                term.Col = rowCol;
            }
            else if (scopeAxis == ScopeRangeAxis.Rows)
            {
                //if it is an open table 
                // 1. find the key of the row
                // 2. find the row of based on the key value
                // same for filter 

                var isOpenTbl = IsOpenTable(ConfigObject, term.TableCode);
                if (isOpenTbl)
                {
                    if (term.TableCode == scopeTableCode)
                    {
                        term.Row = rowCol;
                    }
                    else
                    {
                        var linkingDetails = GetLinkingDim(term.TableCode);
                        var factInMaster = FindFactInRowOfMasterTable(linkingDetails.FK_TableDim, scopeTableCode, rowCol);
                        //term.Row = keyFact1 is null ? "" : FindRowUsingForeignKeyInDetailTbl(tableRel.FK_TableDim, term.TableCode, keyFact1.TextValue);
                        term.Row = factInMaster is null ? "" : FindRowUsingForeignKeyInDetailTbl(linkingDetails.FK_TableDim, term.TableCode, factInMaster.TextValue);
                    }
                }
                else
                {
                    term.Row = rowCol;
                }
            }


        }

        private MTableKyrKeys GetLinkingDim(string tableCode)
        {

            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);
            var sqlSelect = @"
                SELECT
                  tk.TableCode
                 ,tk.TableCodeKeyDim                 
                 ,tk.FK_TableDim
                FROM dbo.mTableKyrKeys tk
                WHERE tk.TableCode = @tableCode
                ";
            var rel = connectionEiopa.QuerySingle<MTableKyrKeys>(sqlSelect, new { tableCode });
            return rel;
        }

        private bool GetConfiguration()
        {

            ConfigObject = Configuration.GetInstance(SolvencyVersion).Data;
            if (string.IsNullOrEmpty(ConfigObject.LoggerValidatorFile))
            {
                var errorMessage = "LoggerValidatorFile is not defined in ConfigData.json";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(ConfigObject.LoggerValidatorFile, rollOnFileSizeLimit: true, shared: true, rollingInterval: RollingInterval.Day)
                .CreateLogger();


            if (!Configuration.IsValidVersion(SolvencyVersion))
            {
                var errorMessage = $"Excel Writer --Invalid Eiopa Version: {SolvencyVersion}";
                Console.WriteLine(errorMessage);
                Log.Error(errorMessage);
            }

            //the connection strings depend on the Solvency Version
            if (string.IsNullOrEmpty(ConfigObject.EiopaDatabaseConnectionString) || string.IsNullOrEmpty(ConfigObject.LocalDatabaseConnectionString))
            {
                var errorMessage = "Empty ConnectionStrings in ConfigData.json file";
                Console.WriteLine(errorMessage);
                throw new SystemException(errorMessage);
            }

            return true;
        }


        private bool ValidateOpenTableKeysUnique(int documentId)
        {
            using var connectionLocal = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            var sqlOpenTables = "select sheet.TemplateSheetId,sheet.SheetCode,sheet.TableCode from TemplateSheetInstance sheet where sheet.InstanceId = @DocumentId and sheet.IsOpenTable = 1;";
            var openSheets = connectionLocal.Query<TemplateSheetInstance>(sqlOpenTables, new { documentId });
            var isValid = true;
            var errorCounter = 0;
            foreach (var sheet in openSheets)
            {

                var sqlTblKyr = @"SELECT  tk.TableCode ,tk.TableCodeKeyDim ,tk.FK_TableDim FROM dbo.mTableKyrKeys tk WHERE tk.TableCode = @tableCode";
                var tblKyr = connectionEiopa.QuerySingle<MTableKyrKeys>(sqlTblKyr, new { sheet.TableCode });
                if (string.IsNullOrWhiteSpace(tblKyr.TableCodeKeyDim))
                {
                    continue;
                }

                var sqKeyDim = @"	
                        SELECT 
                        map.DYN_TAB_COLUMN_NAME	   as columnCode	   	  
	                    FROM MAPPING map
	                    left outer join mTable tab on tab.TableID= map.TABLE_VERSION_ID
	                    where
	                    tab.TableCode= @tableCode
	                    AND map.ORIGIN = 'C'    
	                    and map.dim_code like @keyDimension
                    ";
                var keyDimension = $"%{tblKyr.TableCodeKeyDim.Trim()}%";

                var KeyColumn = connectionEiopa.QuerySingleOrDefault<string>(sqKeyDim, new { sheet.TableCode, keyDimension })??"";
                if (string.IsNullOrEmpty(KeyColumn))
                {
                    continue;
                }


                var sqlDuplicate = "select  fact.TextValue  from TemplateSheetFact fact where fact.TemplateSheetId=@sheetId and fact.Col=@keyColumn group by TextValue having count(*) >1";
                var duplicateText = connectionLocal.QueryFirstOrDefault<string>(sqlDuplicate, new { sheetId = sheet.TemplateSheetId, KeyColumn });

                if (!string.IsNullOrWhiteSpace(duplicateText))
                {
                    errorCounter = +1;
                    isValid = false;
                    var errorRule = new ERROR_Rule
                    {
                        RuleId = 0,
                        ErrorDocumentId = documentId,
                        SheetId = sheet.TemplateSheetId,
                        SheetCode = sheet.SheetCode,
                        Scope=sheet.SheetCode,
                        RowCol = KeyColumn,
                        RuleMessage = $"Duplicate Key. Column:{KeyColumn} value:{duplicateText} ",
                        IsWarning = false,
                        IsError = true,
                        IsDataError = true,
                        Row = "",
                        Col = KeyColumn,
                        DataValue = duplicateText,
                        DataType = ""
                    };
                    CreateRuleError(errorRule);
                }
            }

            Log.Information($"Unique Keys Number of Errors :{errorCounter}");
            return isValid;
        }

        private bool HasEmptySheets(int documentId)
        {
            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var isValid = true;

            var sqlSheets = "select sheet.TemplateSheetId,sheet.SheetCode from TemplateSheetInstance sheet where sheet.InstanceId = @DocumentId ";
            var sheets = connectionPension.Query<(int sheetId, string sheetCode)>(sqlSheets, new { documentId });
            if (sheets is null)
            {
                return false;
            }

            foreach (var (sheetId, sheetCode) in sheets)
            {
                var sqlCountValid = @" SELECT COUNT(*) cnt FROM TemplateSheetFact fact WHERE  fact.IsEmpty = 0 AND fact.TemplateSheetId = @sheetId";
                var countValidFacts = connectionPension.QuerySingleOrDefault<int>(sqlCountValid, new { sheetId });

                if (countValidFacts == 0)
                {

                    isValid = false;
                    var errorRule = new ERROR_Rule
                    {
                        RuleId = 10400,
                        ErrorDocumentId = documentId,
                        SheetId = sheetId,
                        SheetCode = sheetCode,
                        RowCol = "",
                        RuleMessage = $"All the cells of the sheet are EMPTY. SheetId:{sheetId} SheetCode:{sheetCode} ",
                        IsWarning = false,
                        IsError = true,
                        IsDataError = true,
                        Row = "",
                        Col = "",
                        DataValue = "",
                        DataType = ""
                    };
                    CreateRuleError(errorRule);
                    var message = $"All the cells of the sheet are EMPTY. SheetId:{sheetId} SheetCode:{sheetCode} ";
                    Log.Error(message);

                }
            }


            return isValid;
        }

        private bool ValidateFactEnumValues()
        {
            var errorCounter = 0;
            using var connenctionLocal = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            //


            var sqlDocumentFacts = @"
            SELECT 
                   sheet.InstanceId
	              ,[FactId]
	              ,sheet.TemplateSheetId
                  ,sheet.SheetCode
	              ,[Row]
                  ,[Col]
	              ,fact.[TableID]
	              ,[TextValue]      
                  ,[NumericValue]      		        
	              ,[CellID]
	              ,[IsShaded]
                  ,IsRowKey	        
                  ,[DataType]
	              ,fact.DataTypeUse  
                  ,[MetricId]
                   ,XBRLCode
                ,[IsConversionError]
              FROM TemplateSheetFact fact
              LEFT OUTER JOIN TemplateSheetInstance sheet on sheet.TemplateSheetId=fact.TemplateSheetId
              where 
              sheet.InstanceId =@documentId                  
              order by sheet.SheetCode, fact.Row, fact.Col
            ";



            var facts = connenctionLocal.Query<TemplateSheetFact>(sqlDocumentFacts, new { DocumentId }).ToList();
            if (facts is null)
            {
                Log.Error($"Document : {DocumentId} has zero facts");
                return false;
            }
            foreach (var fact in facts)
            {

                if (fact.IsConversionError)
                {
                    errorCounter += 1;
                    var errorRule = new ERROR_Rule
                    {
                        RuleId = 10100,
                        ErrorDocumentId = DocumentId,
                        SheetId = fact.TemplateSheetId,
                        SheetCode = fact.SheetCode,
                        Scope = fact.SheetCode,
                        RowCol = $"{fact.Row}/{fact.Col}",
                        RuleMessage = $" Factid:{fact.FactId} Data Conversion Error",
                        IsWarning = false,
                        IsError = true,
                        IsDataError = true,
                        Row = fact.Row,
                        Col = fact.Col,
                        DataValue = fact.TextValue,
                        DataType = fact.DataType
                    };
                    CreateRuleError(errorRule);
                }


                if (fact.DataTypeUse == "E" && !string.IsNullOrEmpty(fact.TextValue) && !fact.IsRowKey)
                {
                    var mMember = FindMemberInHierarchy(fact.MetricID,   fact.TextValue,fact.XBRLCode);
                    if (mMember is null)
                    {
                        var validValues = GetAllMetricValidValues(fact.MetricID);
                        var validValuesStr = string.Join(",", validValues);

                        var errorRule = new ERROR_Rule
                        {
                            RuleId = 10101,
                            ErrorDocumentId = DocumentId,
                            SheetId = fact.TemplateSheetId,
                            SheetCode = fact.SheetCode,
                            Scope = fact.SheetCode,
                            RowCol = $"{fact.Row}/{fact.Col}",
                            RuleMessage = $"Invalid ENUM Value:{fact.TextValue} where valid values are :{validValuesStr}-- Factid:{fact.FactId} xbrlCode:{fact.XBRLCode} - {fact.MetricID}",
                            IsWarning = false,
                            IsError = true,
                            IsDataError = true,
                            Row = fact.Row,
                            Col = fact.Col,
                            DataValue = fact.TextValue,
                            DataType = fact.DataType
                        };
                        CreateRuleError(errorRule);
                    }
                }
            }

            var sqlUpdate = @"update ERROR_Document set IsDocumentValid=@isDocumentValid, errorCounter=@errorCounter, WarningCounter=@warningCounter where ErrorDocumentId=@documentId";
            connenctionLocal.Execute(sqlUpdate, new { isDocumentValid = errorCounter == 0, errorCounter, warningCounter = 0, DocumentId });

            Log.Information($"Fact Values Validated. Number of Data Errors:{errorCounter}");


            return (errorCounter == 0);
        }
        private MMember FindMemberInHierarchy(int metricId,  string factTextEnumValue,string xblr)
        {
            //1. the metric was found from the fact xbrl contains the HIERARCHY
            //2. Find the member in the hierarchy which has  the textEnum value
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            var sqlGetMetric = @"select met.ReferencedHierarchyID,met.ReferencedDomainID,ReferencedHierarchyID from mMetric met  where met.MetricID= @metricId";
            var metric = connectionEiopa.QuerySingleOrDefault<MMetric>(sqlGetMetric, new { metricId });
            if (metric is null)
            {
                return null;
            }

            var sqlFindMem = @"
                select mem.MemberID,mem.DomainID,mem.IsDefaultMember,mem.MemberLabel,mem.MemberXBRLCode  
                  FROM mHierarchyNode hi
                  join mMember mem on mem.MemberID= hi.MemberID
                  where HierarchyID= @hierarchyId
                  and mem.MemberXBRLCode= @factTextEnumValue
                ";
            var member = connectionEiopa.QuerySingleOrDefault<MMember>(sqlFindMem, new { hierarchyId = metric.ReferencedHierarchyID,  factTextEnumValue });
            return member;

        }

        private List<string> GetAllMetricValidValues(int metricId)
        {
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            var sqlGetMetric = @"select met.ReferencedHierarchyID,met.ReferencedDomainID from mMetric met  where met.MetricID= @metricId";
            var metric = connectionEiopa.QuerySingleOrDefault<MMetric>(sqlGetMetric, new { metricId });
            if (metric is null)
            {
                return new List<string>();
            }

            var sqlHierarchyMembers = @"
                select mem.MemberID,mem.DomainID,mem.IsDefaultMember,mem.MemberLabel,mem.MemberXBRLCode  
                  FROM mHierarchyNode hi
                  join mMember mem on mem.MemberID= hi.MemberID
                  where HierarchyID= @hierarchyId;                  
                ";

            var values = connectionEiopa.Query<MMember>(sqlHierarchyMembers, new { hierarchyId = metric.ReferencedHierarchyID })
                .Select(mem => mem.MemberXBRLCode)
                .ToList();
            return values;
        }

        private void CreateErrorDocument()
        {
            //var connectionPensionString = Configuration.GetConnectionPensionString();
            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var sqlDelete = @"delete from ERROR_Document where ErrorDocumentId = @DocumentId";
            connectionPension.Execute(sqlDelete, new { DocumentId });
            var sqlInsert = @"INSERT INTO ERROR_Document( OrganisationId,ErrorDocumentId, UserId)VALUES(@PensionFundId, @DocumentId,  @userId)";
            connectionPension.Execute(sqlInsert, new { DocumentInstance.PensionFundId, DocumentId, userId = DocumentInstance.UserId });

        }

        private void CreateRuleError(ERROR_Rule errorRule)
        {
            //var connectionPensionString = Configuration.GetConnectionPensionString();
            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            var sqlInsert = @"
                INSERT INTO [ERROR_Rule]
                           (
			                [RuleId]
                           ,[ErrorDocumentId]                           
                           ,[SheetId]
                           ,[sheetCode]
                           ,[rowCol]                           
                           ,[RuleMessage]
                           ,[IsError]
                           ,[IsWarning]
                          ,[IsDataError]
                          ,[Row]
                          ,[Col]
                          ,[DataValue]
                           ,[DataType]
                           ,TableBaseFormula
                           ,Filter
                           ,Scope
                    )
                     VALUES
		                (	
		                   @RuleId
                          ,@ErrorDocumentId                           
                          ,@SheetId
                          ,@sheetCode
                          ,@rowCol                           
                          ,@RuleMessage
                          ,@IsError
                          ,@IsWarning
                          ,@IsDataError
                          ,@Row
                          ,@Col
                          ,@DataValue
                        ,@DataType
                        ,@TableBaseFormula
                        ,@Filter
                        ,@Scope

                        )
                ";

            if (errorRule.RuleMessage.Length > 2500)
            {
                errorRule.RuleMessage = errorRule.RuleMessage.Substring(0, 2449);
            }
            if (errorRule.DataValue.Length > 500)
            {
                errorRule.DataValue = errorRule.DataValue.Substring(0, 499);
            }

            connectionPension.Execute(sqlInsert, errorRule);

        }

        private TemplateSheetFact FindFactInRowOfMasterTable(string keyDim, string tableCode, string row)
        {
            if (keyDim is null)
            {
                return null;
            }
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            //@@@@@@@@@@@@ checck if tableID is available
            var sqKeyColumn = @"	
                        SELECT 
                        map.DYN_TAB_COLUMN_NAME	   as columnCode	   	  
	                    FROM MAPPING map
	                    left outer join mTable tab on tab.TableID= map.TABLE_VERSION_ID
	                    where
	                    tab.TableCode= @tableCode
	                    AND map.ORIGIN = 'C'    
	                    and map.dim_code like @keyDimension
                    ";
            var keyDimension = $"%{keyDim.Trim()}%";
            var KeyColumn = connectionEiopa.QuerySingleOrDefault<string>(sqKeyColumn, new { tableCode, keyDimension });

            var keyFact = GetFact(tableCode, row, KeyColumn);
            return keyFact;
        }

        private string FindRowUsingForeignKeyInDetailTbl(string keyDim, string tableCode, string keyFactValue)
        {
            //find the row of the keyfact which has the value  passed in the parameters (keyFactValue)
            //the column of the key fafact is found using MAPPINGS
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            //since dim is "UI" => find the col "C0040" for example
            var sqKeyColumn = @"	
                        SELECT 
                        map.DYN_TAB_COLUMN_NAME	   as columnCode	   	  
	                    FROM MAPPING map
	                    left outer join mTable tab on tab.TableID= map.TABLE_VERSION_ID
	                    where
	                    tab.TableCode= @tableCode
	                    AND map.ORIGIN = 'C'    
	                    and map.dim_code like @keyDimension
                    ";
            var keyDimension = $"%{keyDim.Trim()}%";
            var KeyCol = connectionEiopa.QuerySingleOrDefault<string>(sqKeyColumn, new { tableCode, keyDimension });


            var sqlKeyFact = @"
                    SELECT fact.Row
                    FROM TemplateSheetFact fact
                    JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId = fact.TemplateSheetId
                    WHERE 
                        sheet.InstanceId = @DocumentId
	                    AND sheet.TableCode = @tableCode
	                    AND fact.Col = @keyCol
                        AND fact.TextValue = @KeyFactValue
                    ";

            //Very strange becauese we may have more than one !! Take the first anyway !!!
            //var fact = connectionInsurance.QuerySingleOrDefault<TemplateSheetFact>(sqlKeyFact, new { DocumentId, tableCode, KeyCol, keyFactValue });
            var fact = connectionInsurance.QueryFirstOrDefault<TemplateSheetFact>(sqlKeyFact, new { DocumentId, tableCode, KeyCol, keyFactValue });

            return fact?.Row ?? "";
        }


        private static bool IsOpenTable(ConfigObject configObj, string tablecode)
        {
            using var connectionEiopa = new SqlConnection(configObj.EiopaDatabaseConnectionString);
            var sqlOpenTable = @"select tab.TableCode from mTable tab where  tab.TableCode= @tableCode and  (YDimVal is null or YDimVal='')";
            var closedTable = connectionEiopa.QuerySingleOrDefault<string>(sqlOpenTable, new { tablecode });
            return closedTable is null;
        }

        public TemplateSheetFact GetFact(string sheetCode, string row, string col)
        {
            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqlFact = @"
                    SELECT fact.TemplateSheetId, sheet.sheetCode, fact.FactId, fact.Row, fact.Col, fact.TextValue, fact.NumericValue, fact.DateTimeValue, fact.DataType
                    FROM TemplateSheetFact fact
                    LEFT OUTER JOIN TemplateSheetInstance sheet
	                    ON sheet.TemplateSheetId = fact.TemplateSheetId
                    WHERE sheet.InstanceId = @DocumentId
	                    AND sheet.TableCode = @sheetCode
	                    AND fact.Col = @col
                        AND fact.Row = @row
                ";
            var valueFact = connectionInsurance.QuerySingleOrDefault<TemplateSheetFact>(sqlFact, new { DocumentId, sheetCode, row, col });
            return valueFact;
        }

        private decimal FunctionForSumTermForCloseTableNew(RuleStructure rule, RuleTerm sumTerm)
        {

            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);

            //sum({S.25.01.01.01,r0010-0070,c0030})
            //sum({S.23.01.01.01, r0030-0050}) and scope detail is "S.23.01.01.01 (c0010)" 
            //sum({SR.27.01.01.01, c0030, (r0310-0330)})
            //YES 994 rule, this is also valid  sum({S.02.02.01.02, c0050, snnn} scope :S.02.02.01.01 (r0020-0200)
            var sumObj = SumTermParser.ParseTerm(sumTerm.TermText);
            var sqlSum = "";

            var sqlAdd = rule.ScopeTableCode.Trim() == sumTerm.TableCode.Trim()
                    ? " and sheet.TemplateSheetId = @sheetId "
                    : " and sheet.tableCode = @tableCode ";

            if (sumObj.RangeAxis == VldRangeAxis.Rows)
            {
                sqlSum = @"
                    SELECT SUM(Coalesce(FACT.NumericValue, 0)) total
                    FROM TemplateSheetFact fact
                    LEFT OUTER JOIN TemplateSheetInstance sheet
	                    ON sheet.TemplateSheetId = fact.TemplateSheetId
                    WHERE sheet.InstanceId = @DocumentId
                        and fact.Row BETWEEN @startRowCol and @endRowCol	                    
	                    and fact.Col = @fixedRowCol
                ";                

                sqlSum += sqlAdd;

                var fixedRowCol = sumObj.RangeAxis == VldRangeAxis.Cols ? sumTerm.Row : sumTerm.Col;
                var sum = connectionPension.QuerySingleOrDefault<decimal?>(sqlSum, new { sheetId = sumTerm.SheetId, tableCode=sumTerm.TableCode,  startRowCol = sumObj.StartRowCol, endRowCol = sumObj.EndRowCol, fixedRowCol, DocumentId }) ?? 0;
                return sum;

            }
            else if (sumObj.RangeAxis == VldRangeAxis.Cols)
            {
                sqlSum = @"
                    SELECT SUM(Coalesce(FACT.NumericValue, 0)) total
                    FROM TemplateSheetFact fact
                    LEFT OUTER JOIN TemplateSheetInstance sheet
	                    ON sheet.TemplateSheetId = fact.TemplateSheetId
                    WHERE sheet.InstanceId = @DocumentId
                        and fact.Col BETWEEN @startRowCol and @endRowCol	                    
	                    AND fact.Row = @fixedRowCol
                ";
                sqlSum += sqlAdd;

                var fixedRowCol = sumObj.RangeAxis == VldRangeAxis.Cols ? sumTerm.Row : sumTerm.Col;
                var sum = connectionPension.QuerySingleOrDefault<decimal?>(sqlSum, new { sheetId = sumTerm.SheetId, tableCode = sumTerm.TableCode, startRowCol = sumObj.StartRowCol, endRowCol = sumObj.EndRowCol, fixedRowCol, DocumentId }) ?? 0;
                return sum;

            }
            else if (sumObj.RangeAxis == VldRangeAxis.None)
            {
                //Rule 994 scope :S.02.02.01.01 (r0020-0200) formula {S.02.02.01.01, c0020} = {S.02.02.01.01, c0030} + {S.02.02.01.01, c0040} + sum({S.02.02.01.02, c0050, snnn})  
                //we create many document Rules (from r0020 to r0200 ) but each document rule will take its fixed row for snnn
                sqlSum = @"
                    SELECT SUM(Coalesce(FACT.NumericValue, 0)) total
                    FROM TemplateSheetFact fact
                    LEFT OUTER JOIN TemplateSheetInstance sheet
	                    ON sheet.TemplateSheetId = fact.TemplateSheetId
                    WHERE sheet.InstanceId = @DocumentId                        
                        and fact.Row  = @Row	                    
	                    AND fact.Col = @col
                ";
                sqlSum += sqlAdd;
                
                var sum = connectionPension.QuerySingleOrDefault<decimal?>(sqlSum, new { DocumentId, sheetId = sumTerm.SheetId, tableCode = sumTerm.TableCode, sumTerm.Row, sumTerm.Col }) ?? 0;
                return sum;
            }
            return 0;
        }

        private decimal FunctionForOpenSumNew(RuleTerm sumTerm, string filterFormula)
        {
            //  rule 929, term = sum({S.06.02.01.01,c0170,snnn})	
            //  filter = matches({S.06.02.01.02,c0290},"^..((91)|(92)|(94)|(99))$") and ({S.06.02.01.01,c0090}=[s2c_LB:x91])
            // for each row of the table, add the rowfacts  which pass the filter 
            // for each rowfact, create a RULE based on the filter which will be evaluated to filter out the rowfact 

            using var connectionInsurance = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            var sqlSumFacts = @"
                    SELECT fact.TemplateSheetId, sheet.tableCode as tableCodeDerived,  sheet.sheetCode, fact.FactId, fact.Row, fact.Col, fact.TextValue, fact.NumericValue, fact.DateTimeValue, fact.DataType
                    FROM TemplateSheetFact fact
                    JOIN TemplateSheetInstance sheet
	                    ON sheet.TemplateSheetId = fact.TemplateSheetId
                    WHERE sheet.InstanceId = @DocumentId
                        AND fact.InstanceId = @DocumentId
	                    AND sheet.tableCode = @tableCode
	                    AND fact.Col = @col
                ";

            var isOpenTbl = IsOpenTable(ConfigObject, sumTerm.TableCode);

            var sumfacts = connectionInsurance.Query<TemplateSheetFact>(sqlSumFacts, new { DocumentId, tableCode = sumTerm.TableCode, col = sumTerm.Col });
            decimal factSum = 0;
            foreach (var sumFact in sumfacts)
            {
                var fakeFilterRule = new RuleStructure(filterFormula, "")//the filter formula will now be the tablebase formula
                {
                    ScopeTableCode = sumFact.TableCodeDerived,
                    SheetId = sumFact.TemplateSheetId,
                    //ValidationRuleId=-sumTerm.rulu

                };

                //Create a RULE for filter formula                
                //--update each term of the rule with a ROW                
                var filterPlainTerms = fakeFilterRule.RuleTerms.Where(term => !term.IsFunctionTerm);
                foreach (var filterTerm in filterPlainTerms)
                {
                    //****************************
                    //a filter term may have a different  tablecode than the sumTerm. Find the linked row                    
                    //  rule 929, term = sum({S.06.02.01.01,c0170,snnn})	
                    //  filter = matches({S.06.02.01.02,c0290},"^..((91)|(92)|(94)|(99))$") and ({S.06.02.01.01,c0090}=[s2c_LB:x91])                                        

                    UpdateTermRowCol(filterTerm, fakeFilterRule.ScopeTableCode, ScopeRangeAxis.Rows, sumFact.Row);
                }
                //evaluate the filter RULE to decide when to add the row@@ add the ruleId tot the temp
                if (string.IsNullOrEmpty(fakeFilterRule.TableBaseFormula))
                {
                    factSum += sumFact.NumericValue;
                    continue;
                }
                UpdateRuleAndFilterTerms(fakeFilterRule);
                

                if ((bool)RuleStructure.AssertIfThenElseExpression(0, fakeFilterRule.SymbolFinalFormula, fakeFilterRule.RuleTerms))
                {
                    factSum += sumFact.NumericValue;
                }

            }
            return factSum;
        }

        private string FunctionForFtdvValue(List<RuleTerm> allTerms, RuleTerm ftdvTerm)
        {

            //get the column of the key cell. the cell in this row which has a value s2c_dim:UI.                     
            //ftdv({PF.06.02.26.02,c0230},"s2c_dim:UI" => PF.06.02.26.02, c0230,s2c_dim:UI  

            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            var ftdvText = ftdvTerm.TermText;

            var rgxTerms = @"ftdv\((.*?),""(.*?)""\)";//ftdv(X0,"s2c_dim:UI") => X0, "s2c_dim:UI"
            var parts = GeneralUtils.GetRegexSingleMatchManyGroups(rgxTerms, ftdvText);
            if (parts.Count != 3)
            {
                Log.Error($"Ftdv error sheetCode:{ftdvTerm.TableCode},row:{ftdvTerm.Row} ftdv{ftdvText}");
                return "";
            }

            var termLetter = parts[1];
            var term = allTerms.FirstOrDefault(term => term.Letter == termLetter);

            var dimLike = $"{parts[2]}%";


            var sqlKeyColMapping = @"
                SELECT map.DYN_TAB_COLUMN_NAME AS columnCode, map.DIM_CODE
                FROM MAPPING map
                LEFT OUTER JOIN mTable tab
	                ON tab.TableID = map.TABLE_VERSION_ID
                WHERE tab.TableCode = @tableCode
                    AND map.DIM_CODE LIKE @dimLike	                
                    AND map.ORIGIN = 'C'
                    AND DYN_TAB_COLUMN_NAME LIKE 'C%'	                	                
                ";


            var keyCol = connectionEiopa.QuerySingleOrDefault<string>(sqlKeyColMapping, new { term.TableCode, dimLike });

            var fValue = GetCellValueFromDbNew(ConfigObject, DocumentId, term.TableCode, term.Row, keyCol);

            return fValue.TextValue;
        }

        private string FunctionForExDimVal(List<RuleTerm> allTerms, RuleTerm exTerm)
        {

            //ExDimVal({S.25.01.01.02,r0220,c0100},AO)=x0
            //check if the cell has the dim AO with value x0.
            //if the cell does not have the Dim, then get the default member of the Dim

            using var connectionLocal = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);

            var sqlDim = @"
                SELECT
                  fdim.FactId
                 ,fdim.Dim
                 ,fdim.Dom
                 ,fdim.DomValue
                 ,fdim.Signature
                 ,fdim.FactDimId
                 ,fdim.IsExplicit
                FROM dbo.TemplateSheetFactDim fdim
                WHERE fdim.FactId = @factId
                AND fdim.Dim = @dim
                ";

            var sqlDefaultMember = @"
                    SELECT
                      mem.MemberCode
                     ,mem.DomainID
                     ,MemberXBRLCode
                     ,IsDefaultMember
                    FROM mMember mem
                    JOIN mDomain dom
                      ON dom.DomainID = mem.DomainID
                    JOIN mDimension dim
                      ON dim.DomainID = dom.DomainID
                    WHERE 1 = 1
                    AND IsDefaultMember = 1
                    AND dim.DimensionCode = @dim
                    ";

            var termText = exTerm.TermText;

            var rgxTerms = @"ExDimVal\((.*?),(.*?)\)";//ExDimVal(X0,AO)=> X0, A0
            var parts = GeneralUtils.GetRegexSingleMatchManyGroups(rgxTerms, termText);
            if (parts.Count != 3)
            {
                Log.Error($"ExDimVal error sheetCode:{exTerm.TableCode},row:{exTerm.Row} ftdv{termText}");
                return "";
            }
            var termLetter = parts[1];
            var term = allTerms.FirstOrDefault(term => term.Letter == termLetter);
            var dim = parts[2];

            var factDim = connectionLocal.QueryFirstOrDefault<TemplateSheetFactDim>(sqlDim, new { term.FactId, dim });
            //if the fact does not have the dim, then get the default dim
            var domAndValue = factDim is null
                ? connectionEiopa.QueryFirstOrDefault<MMember>(sqlDefaultMember, new { dim })?.MemberCode ?? ""
                : GeneralUtils.GetRegexSingleMatch(@".*\((.*?)\)", factDim.Signature); //"s2c_dim:OC(s2c_CU:USD)"=> s2c_CU:USD 

            return domAndValue;
        }



        private static double FunctionForExp(List<RuleTerm> allTerms, RuleTerm exTerm)
        {
            //2^(3.1/5.2) 
            //In a fractional exponent, the numerator is the power to which the number should be taken and the denominator is the root which should be taken.

            //4743	BV908-5	S.01.01.01.01	if ({S.01.01.01.01,r0510,c0010}=[s2c_CN:x1]) then {S.26.02.01.01,r0400,c0080}
            //=exp({S.26.02.01.01,r0100,c0080}*({S.26.02.01.01,r0100,c0080}+0.75*{S.26.02.01.01,r0300,c0080})+{S.26.02.01.01,r0300,c0080}*(0.75*{S.26.02.01.01,r0100,c0080}+{S.26.02.01.01,r0300,c0080}),1,2)
            // $b = exp($c * ($c + 0.75 * $e) + $e * (0.75 * $c + $e),1,2)

            var allTermsDict = allTerms.ToDictionary(term => term.Letter, term => (double)(term.DecimalValue));
            var expTerms = GeneralUtils.GetRegexSingleMatch(@"exp\((.*)\)", exTerm.TermText).Split(",");

            if (expTerms.Length != 3)
            {
                return 0;
            }

            var value = Eval.Execute<double>(expTerms[0], allTermsDict);
            var powerNominator = Eval.Execute<double>(expTerms[1], allTermsDict);
            var powerDenominator = Eval.Execute<double>(expTerms[2], allTermsDict);
            var res = Math.Pow(value, powerNominator / powerDenominator);

            return (double)res;
        }



        private MModule GetModuleId()
        {
            using var connectionPension = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            using var connectionEiopa = new SqlConnection(ConfigObject.EiopaDatabaseConnectionString);



            var sqlSelectDoc = @"SELECT mo.ModuleID, moduleCode, mo.ModuleLabel, mo.XBRLSchemaRef FROM mModule mo where mo.ModuleID = @moduleId";
            var module = connectionEiopa.QuerySingleOrDefault<MModule>(sqlSelectDoc, new { DocumentInstance.ModuleId });
            if (module is null)
            {
                var message = $"Validator: Module NOT Valid. Module: {ModuleId} ";
                Log.Error(message);
                var trans = new TransactionLog()
                {
                    PensionFundId = DocumentInstance.PensionFundId,
                    ModuleCode = DocumentInstance.ModuleCode,
                    ApplicableYear = DocumentInstance.ApplicableYear,
                    ApplicableQuarter = DocumentInstance.ApplicableQuarter,
                    Message = message,
                    UserId = 0,
                    ProgramCode = ProgramCode.VA.ToString(),
                    ProgramAction = ProgramAction.INS.ToString(),
                    InstanceId = DocumentId,
                    MessageType = MessageType.ERROR.ToString()
                };
                TransactionLogger.LogTransaction(SolvencyVersion, trans);
                return module;
            }
            return module;

        }


        private bool IsTableInDocument(string tableCode)
        {
            using var connectionLocal = new SqlConnection(ConfigObject.LocalDatabaseConnectionString);
            var sqlSelectSheet = @"select sheet.TemplateSheetId from TemplateSheetInstance sheet where sheet.InstanceId= @documentId and sheet.TableCode= @tableCode";
            var sheet = connectionLocal.QueryFirstOrDefault<TemplateSheetInstance>(sqlSelectSheet, new { DocumentId, tableCode });
            return sheet is not null;

        }


    }



}

