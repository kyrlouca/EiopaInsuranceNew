using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace EiopaConstants
{

    //testing Constants
    //N-,E-,S-,D- ,P-,M-,B- ,I-
    public enum DataTypesEnumXbrl { Decimal, Integer, String, Boolean, Percent, Enum, Monetary, Date, DomainMember, Error };


    public enum DataTypeMajorUU { StringDtm, NumericDtm, BooleanDtm, DateDtm, UnknownDtm };
    public enum CntTypedExplicit
    {
        Typed, Explicit
    }



    public static class CntConstants
    {

        public static Dictionary<string, string> SimpleDataTypes { get; } = new()
        {
            //these are the values present in mMetric
            { "Boolean", "B" },
            { "TRUE", "B" },
            { "Date", "D" },
            { "Decimal", "N" },
            { "Enumeration/Code", "E" },
            { "Integer", "N" },
            { "Monetary", "M" },
            { "Percent", "P" },
            { "String", "S" },
            { "URI", "S" }
        };


        public static string GetSimpleDataType(string longDataType)
        {
            //these are the values present in mMetric
            var res = longDataType switch
            {
                "Boolean" => "B",
                "TRUE" => "B",
                "Date" => "D",
                "Decimal" => "N",
                "Enumeration/Code" => "E",
                "Integer" => "N",
                "Monetary" => "M",
                "Percent" => "P",
                "String" => "S",
                "URI" => "S",
                _ => "S"
            };
            return res;

        }


        public static DataTypeMajorUU GetMajorDataType(string dataTypeUse)
        {

            var res = dataTypeUse switch
            {

                //N-,E-,S-,D- ,P-,M-,B- ,I-  => String, Numeric, Boolean, Date
                //map the simpleData types  above to a Major datatype
                "N" => DataTypeMajorUU.NumericDtm,
                "E" => DataTypeMajorUU.StringDtm,
                "S" => DataTypeMajorUU.StringDtm,
                "D" => DataTypeMajorUU.DateDtm,
                "P" => DataTypeMajorUU.NumericDtm,
                "M" => DataTypeMajorUU.NumericDtm,
                "B" => DataTypeMajorUU.BooleanDtm,
                "I" => DataTypeMajorUU.NumericDtm,
                _ => DataTypeMajorUU.UnknownDtm
            };
            return res;
        }


        //private readonly static string xurl = @"http://eiopa.europa.eu/xbrl/s2c/dict/dom/AM";

    }

    public static class RegexConstants
    {
        //?: means non-capturing 
        //(?<!) means negative lookbehind            

        public const string ColRowRegEx = @"[A-Z]{1,3}\d{4}";//c0010, r0010
        public const string TalbeCodeRegEx = @"([A-Z]{1,3}(?:\.\d\d){4})";//SR.01.01.01.01
        public const string TermTextRegEx = @"([A-Z]{1,3}(?:\.\d\d){4})\s*,\s*(r\d{4})\s*,\s*(c\d{4})";//{SR.01.01.01.01,r0920,c0010}
        public static Regex PlainTermRegEx { get; set; } = new(@"{([A-Z]{1,3}(?:\.\d\d){4})\s*,.*?}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static Regex TableCodeRegExP { get; set; } = new(@"([A-Z]{1,3})(\.\d\d){4}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public class FFCntUnit
    {
        public string UserName { get; internal protected set; }
        public string XbrlRef { get; internal protected set; }
        public string XbrlValue { get; internal protected set; }
        public DataTypesEnumXbrl UnitType { get; }
        public bool IsListed { get; set; }//only the listed will be displayed


        public FFCntUnit(string userName, string xbrlCode, string xbrlValue, DataTypesEnumXbrl unitType)
        {
            UserName = userName;
            XbrlRef = xbrlCode;
            XbrlValue = xbrlValue;
            UnitType = unitType;
        }
    }



    public class XbrlDataTypeRecord
    {

        public string DataTypeMetric { get; }
        public string DataTypeChar { get; }
        public DataTypesEnumXbrl DataTypeEnumXbrl { get; }
        public DataTypeMajorUU DataTypeEnumMajor { get; }
        public string XbrlRef { get; }
        public string XbrlValue { get; }

        public XbrlDataTypeRecord(string dataTypeMetric, string dataTypeChar, DataTypesEnumXbrl dataTypeEnumXbrl, DataTypeMajorUU dataTypeEnumMajor, string xbrlRef, string xbrlValue)
        {
            DataTypeMetric = dataTypeMetric;
            DataTypeChar = dataTypeChar;
            DataTypeEnumXbrl = dataTypeEnumXbrl;
            DataTypeEnumMajor = dataTypeEnumMajor;
            XbrlRef = xbrlRef;
            XbrlValue = xbrlValue;
        }
    }


    public static class XbrlDataTypes
    {

        //distinct mMEtric: Boolean,Date,Decimal,Enumeration/Code,Integer,Monetary,Percent,String,TRUE,URI

        //@@@@ do i need this since we have dataTypeUse?? 
        //todo check if pension updates datatypeuse
        //N-,E-,S-,D- ,P-,M-,B- ,I-
        public static Dictionary<string, XbrlDataTypeRecord> Units { get; } = new Dictionary<string, XbrlDataTypeRecord>();
        static XbrlDataTypes()
        {
            Units.Add("F", new XbrlDataTypeRecord("Decimal", "N", DataTypesEnumXbrl.Decimal, DataTypeMajorUU.StringDtm, "d", "decimal"));
            Units.Add("S", new XbrlDataTypeRecord("String", "S", DataTypesEnumXbrl.String, DataTypeMajorUU.StringDtm, "s", "string"));
            Units.Add("I", new XbrlDataTypeRecord("Integer", "N", DataTypesEnumXbrl.Integer, DataTypeMajorUU.NumericDtm, "u", "int"));
            Units.Add("N", new XbrlDataTypeRecord("Numeric", "N", DataTypesEnumXbrl.Decimal, DataTypeMajorUU.NumericDtm, "p", "string"));
            Units.Add("P", new XbrlDataTypeRecord("Percent", "P", DataTypesEnumXbrl.Percent, DataTypeMajorUU.NumericDtm, "p", "xbrli:pure"));
            Units.Add("M", new XbrlDataTypeRecord("Monetary", "N", DataTypesEnumXbrl.Monetary, DataTypeMajorUU.NumericDtm, "u", "iso4217:EUR"));
            Units.Add("B", new XbrlDataTypeRecord("Boolean", "B", DataTypesEnumXbrl.Boolean, DataTypeMajorUU.BooleanDtm, "c", "bool"));
            Units.Add("D", new XbrlDataTypeRecord("Date", "D", DataTypesEnumXbrl.Date, DataTypeMajorUU.DateDtm, "c", "xbrli:dateUnion"));
            Units.Add("E", new XbrlDataTypeRecord(@"Enumeration/Code", "E", DataTypesEnumXbrl.Enum, DataTypeMajorUU.StringDtm, "e", "enum"));
        }

        static public XbrlDataTypeRecord GetXbrlDataType(string DataTypeMetric)
        {
            if (string.IsNullOrWhiteSpace(DataTypeMetric))
            {
                return Units["E"];
            }
            if (!Units.TryGetValue(DataTypeMetric.Trim(), out var outUnit))
            {
                return Units["E"];
            }
            return outUnit;
        }
    }

}
