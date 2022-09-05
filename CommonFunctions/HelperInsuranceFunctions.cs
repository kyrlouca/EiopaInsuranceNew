using System;
using System.Collections.Generic;
using System.Text;
using EntityClasses;
using Dapper;
using Microsoft.Data.SqlClient;
using ConfigurationNs;
using EntityClassesZ;
using GeneralUtilsNs;
using EiopaConstants;
    

namespace HelperInsuranceFunctions
{
    public class DimDom
    {


        public string Dim { get; internal set; } = "";//OC
        public string Dom { get; internal set; } = "";//CU
        public string DomAndVal { get; internal set; } = "";//CU:GBP
        public string DomValue { get; internal set; } = "";//USD
        public string DomAndValRaw { get; internal set; } = "";// s2c_CU:USD
        public string Signature { get; internal set; } //"s2c_dim:OC(s2c_CU:GBP)"
        public bool IsWild { get; internal set; } = false;
        private DimDom() { }
        private void GetTheParts()
        {
            //Signature = @"s2c_dim:OC(s2c_CU:USD)";
            //Signature = @"s2c_dim:OC(ID:USD)";
            //Signature = @"s2c_dim:OC(*[xxxx])";            


            var res = GeneralUtils.GetRegexSingleMatchManyGroups(@"s2c_dim:(\w\w)\((.*?)\)", Signature);
            if (res.Count != 3)
            {
                return;
            }

            Dim = res[1];
            DomAndValRaw = res[2]; 
            var domParts = DomAndValRaw.Split(":");
            if (domParts.Length == 2)
            {                
                DomAndVal = res[2].Replace("s2c_", "");
                Dom = domParts[0].Replace("s2c_", "");

                DomValue = domParts[1];
            }

        }
        private DimDom(string signature)
        {
            Signature = signature;
        }
        public static DimDom GetParts(string signature)
        {
            var dimDom = new DimDom(signature);
            dimDom.GetTheParts();
            return dimDom;
        }


    }

    public class InsuranceData
    {
        //public static string SolvencyVersion => "V250";
        //public static ConfigObject ConfigObjectNew => Configuration.GetInstance(SolvencyVersion).Data;

        public static DateTime GetEndOfPeriod(int year, int quarter)
        {
            var dt = new DateTime(year, 1, 1);
            if (quarter == 0)
            {
                return dt.AddYears(1).AddDays(-1);
            }
            else
            {
                return dt.AddMonths((quarter * 3) - 1).AddDays(-1);
            }


        }




        public static DocInstance GetDocumentById( ConfigObject configObject,  int documentId)
        {
            Console.WriteLine($"in GetDocId : {documentId},dbstring: {configObject.LocalDatabaseConnectionString}");
            using var connectionInsurance = new SqlConnection(configObject.LocalDatabaseConnectionString);
            var emptyDocument = new DocInstance();
            var sqlFund = "select doc.InstanceId, doc.Status,doc.IsSubmitted, doc.ApplicableYear,doc.ApplicableQuarter, doc.ModuleCode,doc.ModuleId, doc.PensionFundId,doc.UserId from DocInstance doc where doc.InstanceId=@documentId";
            DocInstance doc=null;
            try
            {
                doc = connectionInsurance.QueryFirstOrDefault<DocInstance>(sqlFund, new { documentId });
            }
            catch(Exception e)
            {
                Console.WriteLine($"errrffor :{e.Message}");
                return emptyDocument;
            }

            if (doc is null)
            {
                Console.WriteLine($"documentId:{documentId} does not exist");
                return emptyDocument;
            }
            Console.WriteLine($"documentId:{documentId} is valid");
            return doc;

        }


        public static MModule GetModuleByCode(string moduleCode)
        {
            var configObject = Configuration.GetInstance("existing").Data;

            using var connectionEiopa = new SqlConnection(configObject.EiopaDatabaseConnectionString);

            //module code : {ari, qri, ara, ...}
            var sqlModule = @"SELECT
                  mod.ModuleID
                 ,mod.TaxonomyID
                 ,mod.ModuleCode
                 ,mod.ModuleLabel
                 ,mod.ConceptualModuleID
                 ,mod.ConceptID
                 ,mod.XBRLSchemaRef
                 ,mod.IsAggregate
                 ,mod.OrganizationCategory
                 ,mod.DefaultFrequency
                FROM dbo.mModule mod
                WHERE mod.ModuleCode = @ModuleCode";
            var module = connectionEiopa.QuerySingleOrDefault<MModule>(sqlModule, new { moduleCode });
            if (module is null)
            {
                return new MModule();
            }
            return module;
        }

        public static PensionFund GetPensionFund(int PensionFundId)
        {

            var configObject = Configuration.GetInstance("existing").Data;
            using var connectionEforos = new SqlConnection(configObject.BackendDatabaseConnectionString);
            var emptyPpensionFundData = new PensionFund() { PensionFundId = 0, IsLarge = false, Status = "D" };

            var sqlPensionFund = @"  select  org.id , org.OrganizationCategoryID, org.OrganizationStatusID,org.Name, org.Number   from Organization org  where org.id   = @pensionFundId";
            var pensionFundData = connectionEforos.QuerySingleOrDefault<PensionFund>(sqlPensionFund, new { PensionFundId });
            if (pensionFundData is null)
            {
                return emptyPpensionFundData;
            }
            if (pensionFundData.OrganizationStatusID == 3)
            {
                //3 is for deleted
                return emptyPpensionFundData;
            }
            pensionFundData.PensionFundId = pensionFundData.id;
            pensionFundData.IsLarge = pensionFundData.OrganizationCategoryId == 2;
            pensionFundData.Status = pensionFundData.OrganizationCategoryId == 3 ? "I" : "A";
            return pensionFundData;
        }


        public static PensionFund GetEforos()
        {

            //Eforos is just a record in Pension FundTable organization Category=3
            var configObject = Configuration.GetInstance("existing").Data;
            using var connectionEforos = new SqlConnection(configObject.BackendDatabaseConnectionString);
            var sqlEforosOrganization = @"select TOP 1 org.id , org.OrganizationCategoryID  from Organization org  where org.OrganizationCategoryID = 3;";
            var eforosId = connectionEforos.QuerySingleOrDefault<int>(sqlEforosOrganization);
            var eforosData = GetPensionFund(eforosId);
            return eforosData;

        }




    }

    public class TermObject
    {
        public string TermText { get; internal set; }
        public string TableCode { get; internal set; } = "";
        public string Row { get; internal set; } = "";
        public string Col { get; internal set; } = "";
        public bool IsValid { get; internal set; }
        private TermObject() { }
        private TermObject(string termText)
        {
            TermText = termText;
         
        }
        private void ParseTheText()
        {
            var termX = GeneralUtils.GetRegexSingleMatchManyGroups(RegexConstants.TermTextRegEx, TermText);
            IsValid = termX.Count == 4;
            if (IsValid)
            {
                TableCode = termX[1].ToUpper();
                Row = termX[2].ToUpper();
                Col = termX[3].ToUpper();
            }
        }
        public static TermObject Parse(string termText)
        {
            var nt= new TermObject(termText);
            nt.ParseTheText();
            return nt;
        }
    }


   
}
