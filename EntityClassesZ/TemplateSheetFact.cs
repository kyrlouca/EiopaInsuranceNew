using GeneralUtilsNs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;


namespace EntityClasses
{
    public class TemplateSheetFact
    {
        public int FactId { get; set; }
        public int TemplateSheetId { get; set; }
        public string Row { get; set; }
        public string Col { get; set; }
        public string Zet { get; set; }
        public int InternalRow { get; set; }
        public int InternalCol { get; set; }
        public string DataPointSignature { get; set; }
        public string DataPointSignatureFilled { get; set; }
        public string OpenRowSignature { get; set; }
        public string Unit { get; set; }
        public int Decimals { get; set; }
        public decimal NumericValue { get; set; }
        public DateTime DateTimeValue { get; set; }
        public bool BooleanValue { get; set; }
        public string TextValue { get; set; }
        public string DPS { get; set; }
        public int CellID { get; set; }
        public string FieldOrigin { get; set; }
        public int TableID { get; set; }
        public bool IsRowKey { get; set; }
        public bool IsShaded { get; set; }
        public string XBRLCode { get; set; }
        public string DataType { get; set; }
        public string DataTypeUse { get; set; }        
        public string SheetCode { get; set; }
        public bool IsEmpty { get; set; }
        public bool IsConversionError { get; set; }
        public string ZetValues { get; set; }
        public string CurrencyDim { get; set; }

        
        public int MetricID { get; set; }
        public string ContextId { get; set; }        
        public string Signature { get; set; }
        public string RowSignature { get; set; }        
        public int InstanceId { get; set; }        
        public string TableCodeDerived { get; set; }


        public TemplateSheetFact() { }

        public TemplateSheetFact UpdateFactDetails(string xbrlCode, List<string> ctxLines) {

            DataPointSignature = BuildFactSignature(xbrlCode,  ctxLines);            

            DataPointSignatureFilled = DataPointSignature;
            if (TextValue.Trim().Length > 1200)
            {
                TextValue = GeneralUtils.TruncateString(TextValue, 1200);
            }

            ConvertTextValue();
            return this;
        }

        private static string BuildFactSignature(string xbrlCode,  List<string> ctxLines)
        {
            //A signature includes the metric and all the dimensions
            //For explicit dimensions (used in open tables, where user can type the value) we do NOT take the value of the context item (add *)


            var metXbrlCode = $"MET({xbrlCode})";

            //var signatureList = contextLines?.Select(line => $"s2c_dim:{line.Dimension}({line.DomainAndValue})").ToList() ?? new List<string>();            
            //signatureList.Sort();

            //signatureList.Insert(0, metXbrlCode);
            //var signature = string.Join("|", signatureList);

            ctxLines.Sort();
            ctxLines.Insert(0, metXbrlCode);
            var newSignature = string.Join("|", ctxLines);
            

            return newSignature;
        }

        public void ConvertTextValue()
        {
            DateTimeValue = new DateTime(1999, 12, 31);            
            if (string.IsNullOrWhiteSpace(TextValue))
            {
                //if spaces it is NOT an error, it was left blank by the user
                IsEmpty = true;
                IsConversionError = false;
                return;
            }

            
            switch (DataTypeUse)
            {
                case "S":
                case "E":
                    //text value is already in textValue field
                    break;
                case "B":
                    BooleanValue = TextValue.Trim().ToUpper() == "TRUE";
                    break;
                case "D":
                    try
                    {
                        DateTimeValue = DateTime.Parse(TextValue);
                    }
                    catch (System.Exception)
                    {
                        IsConversionError = true;
                    }
                    break;
                case "N": //not sure about this
                case "M":
                case "P": //this is a decimal fraction but who cares
                    try
                    {
                        var nfi = new CultureInfo("en-US", false).NumberFormat;
                        NumericValue = Convert.ToDecimal(TextValue, nfi);
                    }
                    catch (System.Exception)
                    {
                        IsConversionError = true;
                    }
                    break;
                case "I":
                    try
                    {
                        NumericValue = Convert.ToInt32(TextValue);
                    }
                    catch (System.Exception)
                    {
                        IsConversionError = true;
                    }
                    break;
                default:
                    break;
            }

        }


    }
}
