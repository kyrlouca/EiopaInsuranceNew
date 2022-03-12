using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Validations
{
    public enum FactTypesXX { Col, Row, RowCol, Error };
    
    public class CellCoordinatesToDelete
    {
        public string InputString { get; set; }        
        public string sheetCode=""; 
        public string row="";
        public string col="";        
        public int documentId = 0;
        public int sheetId = 0;
        public RowColType coordinateType =RowColType.Error;// if cellType is col it means that it has only the Col number and it therefore applies to all rows of the table
        
        public CellCoordinatesToDelete( string inputString)
        {

            InputString = inputString;            
            CreateCellCoordinates();
        }

        public CellCoordinatesToDelete Clone()
        {
            return (CellCoordinatesToDelete)this.MemberwiseClone();
        }

        public void CreateCellCoordinates()
        {
            //parses an expression term which is included in a validation expression
            //some terms have a row, a col, or both            
            //{EP.02.01.30.01, ec0010} OR {EP.02.01.30.01, er0010} OR {EP.02.01.30.01, er0020,ec0010}    
            
            var capitalString = InputString.Trim().ToUpper();

            //first check for Sum
            //sum({PF.06.02.24.01,c0100,snnn})=> "PF.06.02.24.01" , "C0100" 
            var regSum = @"\{([A-Z]{1,3}(?:\.\d\d){4}),\s*?([A-Za-z]{1,2}\d{4}),snnn\}";
            var sumList = GeneralUtilsNs.GeneralUtils.GetRegexSingleMatchManyGroups(regSum, capitalString);
            if (sumList.Count == 3)
            {
                sheetCode = sumList[1];
                col = sumList[2];
                row = "";
                coordinateType = RowColType.Col;
                return;
            }


            //the expression has two mandatory terms and one optional but the capture list will always have 4 items. Or 0 if not match
            //{EP.02.01.30.01, er0010}=> EP.02.01.30.01, er0010,""
            //{PFE.02.01.30.01,r0200,c0040}=> "PFE.02.01.30.01,r0200,c0040" ,"R0220", "C0040"
            var regEx = @"\{([A-Z]{1,3}(?:\.\d\d){4}),\s*?([A-Z]{1,2}\d{4})(?:,(\s*?[A-Z]{1,2}\d{4}))?\}";
            var itemList = GeneralUtilsNs.GeneralUtils.GetRegexSingleMatchManyGroups(regEx, capitalString);
                                                                                                                                  
            if (itemList.Count != 4)// always four becouse you still get a capture for the empty groups
            {
                return ;
            }

            var terms = itemList.Select(item => item.Trim().ToUpper()).ToList();
            sheetCode = terms[1];
            if (string.IsNullOrWhiteSpace(terms[3])) //has only row OR col but not both
            {
                if (terms[2].Contains("R"))
                {
                    row = terms[2];
                    coordinateType = RowColType.Row;
                }
                else if (terms[2].Contains("C"))
                {
                    col = terms[2];
                    coordinateType = RowColType.Col;
                }
            }
            else //has both row and Col
            {
                if (terms[2].Contains("R") && terms[3].Contains("C"))
                {
                    row = terms[2];
                    col = terms[3];
                    coordinateType = RowColType.RowCol;
                }
            }
         
        }

    }


}
