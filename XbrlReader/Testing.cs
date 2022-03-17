using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityClasses;
using Microsoft.Data.SqlClient;
using Dapper;
using GeneralUtilsNs;
using Serilog;
using ConfigurationNs;
using System.Text.RegularExpressions;
using HelperInsuranceFunctions;

namespace XbrlReader
{

    public class RowSignatures
    {
        public string Signature { get; set; }
        public int RowNumber { get; set; }
        public string ZetValue { get; set; }
    }

    class Testing
    {
        public string TestConnectionStr { get; } = "Data Source = KYR-RYZEN\\SQLEXPRESS ; Initial Catalog =test; Integrated Security = true;";
        public string EiopaPension250ConnectionString = "Data Source = KYR-RYZEN\\SQLEXPRESS; Initial Catalog =EIOPA_Unified_DPM_Database_250_Hotfix; Integrated Security = true;";
        public string InsuranceConnectionString = "Data Source = KYR-RYZEN\\SQLEXPRESS ; Initial Catalog =InsuranceDatabase; Integrated Security = true;";
        public int Counter { get; set; }
        public SqlConnection TestConnection { get; set; }

        public Testing()
        {


        }


        public static List<string> ConstructFactFullZetList(string factSignature, string zetSignature)
        {
            //normally zetSignatrue does not contain a Met but there is one case. Do not store it as dimension
            //zetSignature = @"MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:OC(*?[237])|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";
            //factSignature = @"MET(s2md_met:mi289)|s2c_dim:AF(s2c_CA:x1)|s2c_dim:AX(s2c_AM:x4)|s2c_dim:BL(s2c_LB:x73)|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(s2c_CU:EUR)|s2c_dim:RM(s2c_TI:x49)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";

            var zetList = zetSignature.Split("|").ToList();
            var zetOpenList = zetList.Where(dim => dim.Contains("*")).ToList();
            var zetClosedList = zetList.Where(dim => !dim.Contains("*")).ToList(); ;
            var factDims = factSignature?.Split("|")?.ToList() ?? new List<string>();

            var zetFinalList = new List<string>();

            foreach (var zetDim in zetOpenList)
            {

                var zetDimPart = GeneralUtils.GetRegexSingleMatch(@"(s2c_dim.*?:\w\w)", zetDim);//s2c_dim:AF(*?[59]) => s2c_dim:AF
                var factDim = factDims.SingleOrDefault(dim => dim.Contains(zetDimPart));
                if (factDim is not null)
                {
                    var factDimPart = GeneralUtils.GetRegexSingleMatch(@"s2c_dim:(\w\w)", factDim);//"s2c_dim:AF(s2c_CA:x1)=> AF
                    var factDomPart = GeneralUtils.GetRegexSingleMatch(@"s2c_dim:\w\w\((.*?)\)", factDim); //"s2c_dim:AF(s2c_CA:x1)=> s2c_CA:x1                                        
                    zetFinalList.Add($"{factDimPart}#{factDomPart}");
                }
            }

            foreach (var dim in zetClosedList)
            {
                var zetDimPart = GeneralUtils.GetRegexSingleMatch(@"s2c_dim:(\w\w)", dim); //"s2c_dim:TA(s2c_AM:x57)=>TA
                var zetDomPart = GeneralUtils.GetRegexSingleMatch(@"s2c_dim:\w\w\(s2c_(.*?)\)", dim);// "s2c_dim:TA(s2c_AM:x57)=> AM:x57
                if (!string.IsNullOrEmpty(zetDimPart) && !string.IsNullOrEmpty(zetDomPart))
                {
                    zetFinalList.Add($"{zetDimPart}#{zetDomPart}");
                }

            }

            zetFinalList.Sort();
            return zetFinalList;

        }


        public Testing(int init)
        {
            Counter = init;
            TestConnection = new SqlConnection(TestConnectionStr);
        }

        public void Adder(int num)
        {
            //var sqlIns = @"INSERT INTO RowSignature (Signature, RowNumber, ZetValue) VALUES('a2', 1, 'b')";
            //var xx = TestConnection.Execute(sqlIns);
            Counter += num;
        }
        public RowSignatures SelectRow()
        {
            var sqlIns = @"SELECT rs.Signature ,rs.RowNumber ,rs.ZetValue FROM dbo.RowSignature rs";
            var xx = TestConnection.QueryFirst<RowSignatures>(sqlIns);
            return xx;

        }


        public static List<(string, string)> GetRowColMappingsNew()
        {
             var EiopaPension250ConnectionString = "Data Source = KYR-RYZEN\\SQLEXPRESS; Initial Catalog =EIOPA_Unified_DPM_Database_250_Hotfix; Integrated Security = true;";
            using var connectionEiopa = new SqlConnection(EiopaPension250ConnectionString);

            var sqlTab = @"
             select 
                tab.TableID,
                tab.TableCode,                    
                tab.XbrlFilingIndicatorCode,
                tab.XbrlTableCode,
                tab.YDimVal,
                tab.ZDimVal,
                tab.TableLabel
            from mTable tab where tab.TableID= @tableId
            ";
            var table = connectionEiopa.QueryFirstOrDefault<MTable>(sqlTab, new { tableId = 71 });

            //create a list with unique rowCol (dyn_tab_column_name) for the table
            //for each rowCol concatenate its dims
            //---- use the function STRING_AGG which concatenates values from different rows -- makes life much easier
            
            var sqlFieldMappings = @"
                SELECT 
                    DYN_TAB_COLUMN_NAME, 
                    STRING_AGG(cast(DIM_CODE as nvarchar(1000)), '|') as Dims
                FROM MAPPING map
                where map.TABLE_VERSION_ID=@tableId
                and map.DYN_TAB_COLUMN_NAME not like 'PAGE%'
                GROUP BY 
                    DYN_TAB_COLUMN_NAME
                ";
            var fieldMappings = connectionEiopa.Query<(string rowCol, string dims)>(sqlFieldMappings, new { tableId = table.TableID })?.ToList() ?? new List<(string rowCol, string dims)>();
            var isOpenTable = fieldMappings.Any(item => item.rowCol.StartsWith("C"));
            //for both closed and open tables 
            var sqlOutOfTabl = @"
                        select map.DIM_CODE from MAPPING map where 
                        map.ORIGIN='C'
                        and map.IS_IN_TABLE=0
                        and map.TABLE_VERSION_ID =@tableId
                        ";
            var outTableDims = connectionEiopa.Query<string>(sqlOutOfTabl, new { table.TableID })?.ToList() ?? new List<string>();

            var inTableDims = new List<string>();
            if (isOpenTable)
            {
                var sqlInTable = @"
                        select map.DIM_CODE from MAPPING map where 
                        map.ORIGIN='C'
                        and map.IS_IN_TABLE=1
                        and map.IS_PAGE_COLUMN_KEY=0
                        and map.TABLE_VERSION_ID = @tableId
                        ";
                inTableDims = connectionEiopa.Query<string>(sqlInTable, new { table.TableID })?.ToList() ?? new List<string>();
            }

            var fullMappings = new List<(string rowCol, string dims)>();
            foreach (var fieldMapping in fieldMappings)
            {
                var fieldDims = fieldMapping.dims.Split("|")?.ToList() ?? new List<string>();
                fieldDims.AddRange(outTableDims);
                fieldDims.AddRange(inTableDims);
                fieldDims.Sort();
                var dims = string.Join("|", fieldDims);
                fullMappings.Add((fieldMapping.rowCol, dims));
            }

            return fullMappings;
        }



        public static List<string> ConstructZetList(string factSignature, string wildSignature)
        {
            //MET(s2md_met:mi263)|s2c_dim:OC(*[CU_5;x0;0])|s2c_dim:RC(s2c_CU:x4)|s2c_dim:VG(s2c_AM:x80)
            //MET(s2md_met:mi263)|s2c_dim:OC(ab:x1)|s2c_dim:RC(s2c_CU:x4)|s2c_dim:VG(s2c_AM:x80)

            var zetList = new List<string>();
            var factDims = factSignature?.Split("|")?.ToList() ?? new List<string>();
            var wildDims = wildSignature?.Split("|")?.Where(dim => dim.Contains("*"))?.ToList() ?? new List<string>();

            foreach (var wildDim in wildDims)
            {
                var dimPart = GeneralUtils.GetRegexSingleMatch(@"(s2c_dim:\w\w)", wildDim);
                var factDim = factDims.SingleOrDefault(dim => dim.Contains(dimPart));
                if (factDim is not null)
                {
                    var zetParts = GeneralUtils.GetRegexSingleMatchManyGroups(@"s2c_dim:(\w\w)\((.*)\)", factDim);
                    if (zetParts.Count == 3)
                    {
                        zetList.Add($"{zetParts[1]}#{zetParts[2]}");
                    }


                }
            }
            return zetList;

        }


        public static void CleanZet()
        {
            var zet = @"s2c_dim:AX(*[8;1;0])|s2c_dim:AX(x)|s2c_dim:BL(*[331;1512;0])";
            var clean = zet.Split("|").Where(item => !item.Contains("(*")).ToList() ?? new List<string>();
           
        }


        public string FindMapping()
        {

            using var connectionEiopa = new SqlConnection(EiopaPension250ConnectionString);
            var sqlDims = @"
                SELECT 
                    DYN_TAB_COLUMN_NAME, 
                    STRING_AGG(cast(DIM_CODE as nvarchar(max)), '|') as Dims
                FROM MAPPING map
                where map.TABLE_VERSION_ID=@tableId
                and map.DYN_TAB_COLUMN_NAME like 'R%'
                GROUP BY 
                    DYN_TAB_COLUMN_NAME
                ";
            //var dps = @"MET(s2md_met:mi289)|s2c_dim:AF(*?[CA_1])|s2c_dim:AX(*[AM_8;x0;0])|s2c_dim:BL(*[LB_31;x0;0])|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(*?[CU_";

            var dps124 = @"MET(s2md_met:mi289)|s2c_dim:DY(s2c_TI:x1)|s2c_dim:RM(s2c_TI:x46)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";
            var dpsList = dps124.Split("|").ToList();
            //var sqlx = @"select DIM_CODE from MAPPING where TABLE_VERSION_ID=@tableId";
            var mappings = connectionEiopa.Query<(string rowCol, string dims)>(sqlDims, new { tableId = 124 }).ToList();
            var rowCol = "";
            foreach (var mapping in mappings)
            {
                var mapDims = mapping.dims.Split("|").ToList();
                if (mapDims.All(mdim => dpsList.Contains(mdim)))
                {
                    rowCol = mapping.rowCol;
                    break;
                }

            }
            
            return "";

        }

        public void UpdateSheetTabNames(string tableCode)
        {
            using var connectionInsurance = new SqlConnection(InsuranceConnectionString);
            var documentId = 3828;

            var count = 0;
            var sqlSelSheets = @"select TemplateSheetId, SheetCode, TableCode from  TemplateSheetInstance where InstanceId= @documentId and TableCode = @tableCode";
            var sheets = connectionInsurance.Query<TemplateSheetInstance>(sqlSelSheets, new { documentId, tableCode }) ?? new List<TemplateSheetInstance>(); ;
            foreach (var sheet in sheets)
            {

                var sheetCode25 = GeneralUtils.TruncateString(sheet.SheetCode, 25).Replace(":", ";").Trim();
                var sheetTabName = (sheet.SheetCode.Trim() == sheet.TableCode.Trim())
                    ? sheet.SheetCode.Trim()
                    : $"{sheetCode25}#{count++:D2}";


                var sqlUpdSheet = @"update TemplateSheetInstance set SheetTabName= @SheetTabName where TemplateSheetId = @TemplateSheetId";
                connectionInsurance.Execute(sqlUpdSheet, new { SheetTabName = sheetTabName, sheet.TemplateSheetId });
            }


        }

        public static string FindCurrency()
        {
            var extra = "";
            var factSignature = @"MET(s2md_met:mi263)|s2c_dim:BL(s2c_LB:x91)|s2c_dim:MT(s2c_AP:x21)|s2c_dim:OC(s2c_LB:EU)|s2c_dim:RC(s2c_CU:x4)";
            var cellnoOpenDPS = @"MET(s2md_met:mi263)|s2c_dim:BL(s2c_LB:x91)|s2c_dim:MT(*[ab])|s2c_dim:RC(s2c_CU:x4)";
            var cellDims = cellnoOpenDPS.Split("|").ToList();
            var factDims = factSignature.Split("|").ToList();

            if (factDims.Count > cellDims.Count)
            {

                var cellDimswithoutWild = cellDims.Select(item => getWithoutWild(item)).ToList();
                foreach (var factdim in factDims)
                {
                    //check if factim is not in ANY of the cell dims
                    var isFound = cellDimswithoutWild.Any(cellDim => factdim.Contains(cellDim));
                    if (!isFound)
                    {
                        extra = factdim;
                        break;
                    }

                }
                //fact.FactCurrencyValue = extra;
            }

            return extra;

            static string getWithoutWild(string item)
            {
                var cellPart = item.Contains("*") ?
                GeneralUtils.GetRegexSingleMatch(@"(s2c_dim:\w\w)", item)
                : item;
                return cellPart;
            }
        }
        
        

        public static void ExtraZet()
        {

            

            var cellSig = "MET(s2md_met:mi289)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";
            var factSig = "MET(s2md_met:mi289)|s2c_dim:AF(s2c_CA:x1)|s2c_dim:AX(s2c_AM:x4)|s2c_dim:BL(s2c_LB:x52)|s2c_dim:DY(s2c_TI:x8)|s2c_dim:OC(s2c_CU:EUR)|s2c_dim:RM(s2c_TI:x58)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";
            //var tableZet = "MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:OC(*?[237])|s2c_dim:VG(s2c_AM:x80)";

            factSig = @"MET(s2md_met:ei1026)|s2c_dim:BL(s2c_LB:x136)|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(ID:FAC_MON/089/14)|s2c_dim:RD(ID:P_MON/089/14)|s2c_dim:RE(ID:RE_PERSONAL_ACCIDENT)";
            cellSig = @"MET(s2md_met:ei1026)|s2c_dim:FC(*)|s2c_dim:RD(*)|s2c_dim:RE(*)";

            var cellDims = cellSig?.Split("|")?.ToList() ?? new List<string>();
            var factDims = factSig?.Split("|")?.ToList() ?? new List<string>();


            var extraFactDims = factDims.Where(dim => !isFactDimInCellDims(dim))?.ToList() ?? new List<string>();
            

            bool isFactDimInCellDims(string factDim)
            {
                //cell Dim : s2c_dim:CE(*)  OR Dim s2c_dim:NF(*[XB;33;3])  OR  s2c_dim:NF(AC3)
                //fact Dims: MET(s2md_met:ei2426)|s2c_dim:CE(AB:x2)|s2c_dim:NF(AC3)

                var result = cellDims.Any(dim =>  factDim.Contains(cleanCellDim(dim)));
                return result;
            }

            string cleanCellDim(string cellDim)
            {
                var cleanDim = cellDim.Contains("*") ?
                    GeneralUtils.GetRegexSingleMatch(@"(s2c_dim:\w\w)", cellDim)
                    : cellDim;
                return cleanDim;
            }



        }

        

    }


}
