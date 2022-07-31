using GeneralUtilsNs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConfigurationNs;
using EiopaConstants;
using System.Text.RegularExpressions;


namespace Validations
{
    public enum ScopeRangeAxis { Rows, Cols, Error, None };



    public class ScopeDetails
    {
        public string ScopeString { get; }
        public string TableCode { get; private set; } = "";
        public List<string> ScopeRowCols { get; private set; } = new List<string>();
        public ScopeRangeAxis ScopeAxis { get; set; } = ScopeRangeAxis.Error;  ////SE.02.01.18.01 (c0021) => C0021  and axis= col


        private ScopeDetails() { }
        private ScopeDetails(string scopeString)
        {

            ScopeString = scopeString.ToUpper();
        }

        public static ScopeDetails Parse(string scopeString)
        {

            //1645	BV272_1-1	S.27.01.01.03 (c0140-0150;0170-0200)	{S.27.01.01.03, r1030} = sum({S.27.01.01.03, r0830-1020})
            //1280	BV252_1-1	S.17.01.01.01 (r0010;0050;0060;0100;0110-0150;0160;0200-0280;0290-0310;0320-0340;0370-0440;0460-0490)
            //--> r001,r0050,..., R0110,R0110,R0120,R0130,R0140,R0150,R0160, R0200,R00210,...
            //** for ranges, we are creating one row in increments of 10. They may actually not exist, but they will be rejected anywy
            var sc = new ScopeDetails(scopeString);
            var emptyList = new List<string>();
            scopeString = scopeString.ToUpper();

            sc.TableCode = GeneralUtils.GetRegexSingleMatch(RegexConstants.TalbeCodeRegEx, scopeString);// PF.01.02.26.02(r0430; 0440; 0450)=> PF.01.02.26.02
            var range = GeneralUtils.GetRegexSingleMatch(@"\((.*)\)", scopeString);//PF.01.02.26.02(r0430; 0440; 0450;0290-0310) => r0430, 0440, 0450,0290-0310
            if (string.IsNullOrEmpty(range))
            {
                sc.ScopeAxis = ScopeRangeAxis.None;
                return sc;
            }

            if (!range.Contains("R") && !range.Contains("C"))
            {
                sc.ScopeAxis = ScopeRangeAxis.Error;
                return sc;
            };

            sc.ScopeAxis = range.Contains("R") ? ScopeRangeAxis.Rows : ScopeRangeAxis.Cols;



            var ranges = range.Split(";")
                .Select(line => Regex.Replace(line, @"[A-Z]", ""))
                .ToList();

            foreach (var rangeLine in ranges)
            {
                var prefix = sc.ScopeAxis == ScopeRangeAxis.Rows ? "R" : "C";
                var parts = rangeLine.Split("-");
                if (parts.Length == 1)
                {
                    sc.ScopeRowCols.Add($"{prefix}{parts[0]}");
                }
                else
                {
                    var startNumber = int.Parse(parts[0]);
                    var endNumber = int.Parse(parts[1]);

                    for (var i = startNumber; i <= endNumber; i += 10)
                    {
                        sc.ScopeRowCols.Add($"{prefix}{i:D4}");
                    }
                }
            }

            return sc;
        }

    }
}
