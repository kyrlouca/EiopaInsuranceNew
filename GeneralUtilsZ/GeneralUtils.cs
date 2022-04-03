using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;


namespace GeneralUtilsNs

{
    public static class GeneralUtils
    {
        public static bool IsMatch(string RegExpression, string inputString)
        {            
            var rgxMet = new Regex(RegExpression, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return rgxMet.IsMatch(inputString);            

        }

        public static string GetRegexSingleMatch(string RegExpression, string inputString)
        {
            //you need at least *ONE* capture
            //return just the value of the group capture or space
            if(inputString is null || RegExpression is null)
            {
                return "";
            }
            var rgxMet = new Regex(RegExpression, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var match = rgxMet.Match(inputString);
            if (match.Success)
            {
                var res = match.Groups[1].Value; 
                return res;
            }
            return "";

        }


        public static List<string> GetRegexSingleMatchManyGroups(string regExpression, string inputString)
        {
            //Single (first match)  match but many group captures
            //return a string list with all group captures
            var list = new List<string>();
            var rgxMet = new Regex(regExpression, RegexOptions.Compiled | RegexOptions.IgnoreCase|RegexOptions.Multiline);
            var match = rgxMet.Match(inputString);
            if (match.Success)
            {
                foreach (Group group in match.Groups)
                    list.Add(group.Value);
            }
            return list;

        }

        

        public static List<string> GetRegexListOfMatches(string regExpression,string inputString)
        {
            //**** assuming we have just ONE group, it will return a collection of matches. 
            //Since we have just ONE capture group we return the value of group 1. group 0 contains the whole match
            //single capture group returns a list of matches
            //  @"\$(\w)" +   @"$a eq $b + $c" =>{a,b,c}
            var rx = new Regex(regExpression, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var matches = rx.Matches(inputString);
            var list = matches.Select(match => match.Groups[1].Value).ToList();
            return list;
        }

        public static List<string> GetRegexListOfMatchesWithCase(string regExpression, string inputString)
        {
            //**** assuming we have just ONE group, it will return a collection of matches. 
            //Since we have just ONE capture group we return the value of group 1. group 0 contains the whole match
            //single capture group returns a list of matches
            //  @"\$(\w)" +   @"$a eq $b + $c" =>{a,b,c}
            var rx = new Regex(regExpression, RegexOptions.Compiled);
            var matches = rx.Matches(inputString);
            var list = matches.Select(match => match.Groups[1].Value).ToList();
            return list;
        }



        public static void TestGroups(string wildString, string userValues )
        {
            //string zSignature = "AO(*[23])";
            //string zValues = "VS(37)|AO(34)|DI(22)";
            Console.WriteLine($"{wildString}{userValues}");
            

        }
        
        public static string TruncateString(this string variable, int Length)
        {
            if (string.IsNullOrEmpty(variable)) return variable;
            return variable.Length <= Length ? variable : variable.Substring(0, Length);
        }
    }
}
