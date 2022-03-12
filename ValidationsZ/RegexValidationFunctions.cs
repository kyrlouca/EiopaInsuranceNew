using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Validations
{
    public enum FunctionTypes { VAL, SUM, MATCHES, ABS, MAX, MIN, COUNT,NILLED, EMPTY, ISFALLBACK, FTDV, EXDIMVAL, EXP, ERR };
    public static class RegexValidationFunctions
    {
        //functionsString = @"exp|count|empty|isfallback|min|max|sum|matches|ftdv|ExDimVal"
        public static string FunctionsString { get; private set; }

        //FunctionTypesRegStr = @"(exp|count|empty|isfallback|min|max|sum|matches|ftdv|ExDimVal)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)";
        public static string FunctionTypesRegStr { get; set; }

        //compiled regex of @"(exp|count|empty|isfallback|min|max|sum|matches|ftdv|ExDimVal)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)";
        public static Regex FunctionTypesRegex { get; set; }
        public static Dictionary<string, FunctionTypes> FunctionTypesEnumDictionary { get; private set; }


        static RegexValidationFunctions()
        {

            FunctionTypesEnumDictionary = Enum.GetValues(typeof(FunctionTypes))
               .Cast<FunctionTypes>()
               .ToDictionary(t => t.ToString(), t => t);

            //functionsString = @"exp|count|empty|isfallback|min|max|sum|matches|ftdv|ExDimVal"
            var str = FunctionTypesEnumDictionary.Select(item => item.Key);
            FunctionsString = string.Join("|", str);

            //FunctionTypesRegStr= @"(exp|count|empty|isfallback|min|max|sum|matches|ftdv|ExDimVal)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)";
            FunctionTypesRegStr = @$"({FunctionsString})\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)";
            FunctionTypesRegex = new(FunctionTypesRegStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        }
    }



}
