using GeneralUtilsNs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Validations
{
    public enum VldRangeAxis { Rows, Cols, None };
    public class SumTermParser
    {
        public string SumText { get; internal set; }
        public string TableCode { get; internal set; }
        public string StartRowCol { get; internal set; } = "";
        public string EndRowCol { get; internal set; } = "";
        public string FixedRowCol { get; internal set; } = "";
        public VldRangeAxis RangeAxis { get; internal set; } = VldRangeAxis.None;
        private string Prefix { get; set; } = "";
        private SumTermParser(string sumText)
        {
            SumText = sumText.ToUpper();
        }
        private SumTermParser() { }
        private void ParseTheTerm()
        {
            //find the start, end range of a sum term
            //we can have a fixed col or a fixed row
            //BV35-1 is one of the trickiest : For each cell in the Row 200=> get the sum of the columns            
            //BV35-1: {S.16.01.01.02, r0200} = sum({S.16.01.01.02, r0040-0190})  Scope:	S.16.01.01.02 (c0020-0080)

            //BV45-1 is one of the trickiest : For each cell in col c0180=> get the sum of the rows
            //BV45-1: {S.17.01.01.01, c0180} = sum({S.17.01.01.01, c0020-0130}) ScopeS.17.01.01.01 (r0020;0030;0070;0080;0170;0180)
            //BV309_2-10: sum({SR.27.01.01.20, c1300, (r3300-3600)})
            //BV252_2-10:  sum({SR.17.01.01.01, r0260, (c0020-0170)})   scope :SR.01.01.07.01                     
            //BV254_1-2-7: sum({S.25.01.01.01,r0010-0070,c0040})        scope :S.01.01.01.01

            var textParts = SumText.Split(",").ToList();
            if (textParts.Count < 1)
            {
                return;
            }
            TableCode = GeneralUtils.GetRegexSingleMatch(EiopaConstants.RegexConstants.TalbeCodeRegEx, textParts[0]);
            textParts.RemoveAt(0);


            var rangePart = textParts.FirstOrDefault(part => part.Contains("-")) ?? "";
            if (string.IsNullOrEmpty(rangePart))
            {
                return;
            }
            var rangeParts = rangePart.Split("-");

            if (rangeParts.Any(part => part.Contains("R")))
            {
                RangeAxis = VldRangeAxis.Rows;
                Prefix = "R";

            }
            else if (rangeParts.Any(part => part.Contains("C")))
            {
                RangeAxis = VldRangeAxis.Cols;
                Prefix = "C";
            }
            else
            {
                return;
            }


            StartRowCol = $"{Prefix}{GeneralUtils.GetRegexSingleMatch(@"(\d{4})", rangeParts[0])}";
            EndRowCol = $"{Prefix}{GeneralUtils.GetRegexSingleMatch(@"(\d{4})", rangeParts[1])}";


            var fixedPart = textParts.FirstOrDefault(part => !part.Contains("-")) ?? "";
            FixedRowCol = GeneralUtils.GetRegexSingleMatch(@"(\w\d{4})", fixedPart);


        }
        public static SumTermParser ParseTerm(string termText)
        {
            var termParser = new SumTermParser(termText);
            termParser.ParseTheTerm();
            return termParser;
        }
    }
}
