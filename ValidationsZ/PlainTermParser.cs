using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeneralUtilsNs;


namespace Validations
{
    public class PlainTermParser
    {
        public string TermText { get; internal set; }
        public string TableCode { get; internal set; }
        public string Row { get; internal set; } = "";
        public string Col { get; internal set; } = "";        
        public bool IsSum { get; internal set; }        

        private PlainTermParser(string termText)
        {
            TermText = termText.Trim().ToUpper();
        }
        private PlainTermParser() { }
        private void ParseTheTerm()
        {
            //{S.01.02.07.01, r0180,c0010} 
            //{S.01.02.04.01, c0010}
            //{S.16.01.01.02, r0200} 
            //{S.16.01.01.02, r0040-0190}
            //{S.17.01.01.01, c0020-0130}
            //{S.25.01.01.01,r0010-0070,c0040}
            //{SR.27.01.01.20, c1300, (r3300-3600)
            //{S.06.02.04.01,c0170,snnn}

            var rTextTerm = GeneralUtils.GetRegexSingleMatch(@"{(.*)}", TermText);
            if (string.IsNullOrEmpty(rTextTerm))
                return;

            var textParts = rTextTerm.Split(",").ToList();
            if (textParts.Count < 2)
                return;


            TableCode = GeneralUtils.GetRegexSingleMatch(EiopaConstants.RegexConstants.TalbeCodeRegEx, textParts[0]);
            textParts.RemoveAt(0);
            Row = textParts.FirstOrDefault(part => part.ToUpper().Contains("R"))?.Trim()??"";
            Col = textParts.FirstOrDefault(part => part.Trim().ToUpper().Contains("C"))?.Trim()??"";
            IsSum = textParts.Any(part => part.ToUpper().Contains("SNN") || part.ToUpper().Contains("-"));

        }

        public static PlainTermParser ParseTerm(string termText)
        {
            var termParser = new PlainTermParser(termText);
            termParser.ParseTheTerm();
            return termParser;
        }

    }
}
