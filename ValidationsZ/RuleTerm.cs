using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EiopaConstants;
using GeneralUtilsNs;
using Validations;

namespace Validations
{
    //public enum FunctionTypes { Val, Sum, Matches, Abs, Max, Min, Cnt, Empty, Fallback, CompareEnum, ExDimVal, xTerm, Err };
    public enum RowColType { Col, Row, RowCol, Error };


    public class RuleTerm
    {
        //{PFE.02.01.30.01,r0020,c0040} or empty({PFE.01.02.31.01, r0100}) or  sum({PFE.06.02.30.01,c0100,snnn})
              

        public bool IsFunctionTerm { get; set; } = false;
        public FunctionTypes FunctionType { get; set; }
        public DataTypeMajorUU DataTypeOfTerm { get; set; }
        public bool BooleanValue { get; set; }
        public string TextValue { get; set; }
        public decimal DecimalValue { get; set; }
        public int NumberOfDecimals { get; set; }
        public DateTime DateValue { get; set; }
        public string Letter { get; }
        public string TermText { get; set; }

        public string Row { get; set; } = "";
        public string Col { get; set; } = "";
        public string TableCode { get; set; }
        public RowColType RowColType { get; set; }

        public int FactId { get; set; }
        public int SheetId { get; set; }
        public bool IsMissing { get; set; }
        public bool IsSum { get; internal set; }
        private RuleTerm() { }



        public RuleTerm(string letter, string textValue, decimal decimalValue, bool booleanValue, DateTime dateValue, DataTypeMajorUU dataType, bool isMissing)
        {
            //used for testing assertExpression
            Letter = letter;
            BooleanValue = booleanValue;
            TextValue = textValue;
            DecimalValue = decimalValue;
            DateValue = dateValue;
            DataTypeOfTerm = dataType;
            IsMissing = isMissing;
        }


        public RuleTerm(string letter, string termText, bool isFunctionTerm)
        {
            Letter = letter;
            TermText = termText;
            IsFunctionTerm = isFunctionTerm;
            FunctionType = isFunctionTerm ? GetFunctionType() : FunctionTypes.VAL;
            if (!isFunctionTerm)
            {
                var normalTerm = PlainTermParser.ParseTerm(termText);
                TableCode = normalTerm.TableCode;
                Row = normalTerm.Row;
                Col = normalTerm.Col;
                IsSum = normalTerm.IsSum;
            }
                
            DataTypeOfTerm = GetValueType(FunctionType);
        }

        public void AssignDbValues(DbValue dbValue)
        {
            FactId = dbValue.FactId;
            TextValue = dbValue.TextValue;
            DecimalValue = dbValue.DecimalValue;
            NumberOfDecimals = dbValue.NumberOfDecimals;
            DateValue = dbValue.DateValue;
            BooleanValue = dbValue.BoolValue;
            DataTypeOfTerm = dbValue.DataTypeEnumMajorUU; //the value type 
            IsMissing = dbValue.IsMissing;
        }

        public FunctionTypes GetFunctionType()
        {
            var match = RegexValidationFunctions.FunctionTypesRegex.Match(TermText);
                        
            var fnType = match.Success
                ? RegexValidationFunctions.FunctionTypesEnumDictionary[match.Groups[1].Value.Trim().ToUpper()]
                : FunctionTypes.ERR;
            
            return fnType;
            
        }

        public RuleTerm Clone()
        {
            var newRuleTerm = (RuleTerm)MemberwiseClone();
            return newRuleTerm;
        }
        private static DataTypeMajorUU GetValueType(FunctionTypes functionType)
        {
            var valueType = functionType switch
            {
                FunctionTypes.SUM => DataTypeMajorUU.NumericDtm,
                FunctionTypes.MATCHES => DataTypeMajorUU.BooleanDtm,
                FunctionTypes.ABS => DataTypeMajorUU.NumericDtm,
                FunctionTypes.MAX => DataTypeMajorUU.NumericDtm,
                FunctionTypes.MIN => DataTypeMajorUU.NumericDtm,
                FunctionTypes.EMPTY => DataTypeMajorUU.BooleanDtm,
                FunctionTypes.ISFALLBACK => DataTypeMajorUU.BooleanDtm,                
                FunctionTypes.EXDIMVAL => DataTypeMajorUU.BooleanDtm,
                _ => DataTypeMajorUU.UnknownDtm
            };
            return valueType;
        }

        public void AssignRowColOld()
        {
            //parses an expression term which is included in a validation expression
            //some terms have a row, a col, or both            
            //{EP.02.01.30.01, ec0010} OR {EP.02.01.30.01, er0010} OR {EP.02.01.30.01, er0020,ec0010}    

            var capitalString = TermText.Trim().ToUpper();

            //first check for Sum
            //sum({PF.06.02.24.01,c0100,snnn})=> "PF.06.02.24.01" , "C0100" 
            var regSum = @"\{([A-Z]{1,3}(?:\.\d\d){4}),\s*?([A-Za-z]{1,2}\d{4}),snnn\}";
            var sumList = GeneralUtilsNs.GeneralUtils.GetRegexSingleMatchManyGroups(regSum, capitalString);
            if (sumList.Count == 3)
            {
                TableCode = sumList[1];
                Col = sumList[2];
                Row = "";
                RowColType = RowColType.Col;
                return;
            }


            //the expression has two mandatory terms and one optional but the capture list will always have 4 items. Or 0 if not match
            //{EP.02.01.30.01, er0010}=> EP.02.01.30.01, er0010,""
            //{PFE.02.01.30.01,r0200,c0040}=> "PFE.02.01.30.01,r0200,c0040" ,"R0220", "C0040"
            var regEx = @"\{([A-Z]{1,3}(?:\.\d\d){4}),\s*?([A-Z]{1,2}\d{4})(?:,(\s*?[A-Z]{1,2}\d{4}))?\}";
            var itemList = GeneralUtilsNs.GeneralUtils.GetRegexSingleMatchManyGroups(regEx, capitalString);

            if (itemList.Count != 4)// always four becouse you still get a capture for the empty groups
            {
                return;
            }

            var terms = itemList.Select(item => item.Trim().ToUpper()).ToList();//change terms to parts
            TableCode = terms[1];
            if (string.IsNullOrWhiteSpace(terms[3])) //has only row OR col but not both
            {
                if (terms[2].Contains("R"))
                {
                    Row = terms[2];
                    RowColType = RowColType.Row;
                }
                else if (terms[2].Contains("C"))
                {
                    Col = terms[2];
                    RowColType = RowColType.Col;
                }
            }
            else //has both row and Col
            {
                if (terms[2].Contains("R") && terms[3].Contains("C"))
                {
                    Row = terms[2];
                    Col = terms[3];
                    RowColType = RowColType.RowCol;
                }
            }

        }


    }
}
