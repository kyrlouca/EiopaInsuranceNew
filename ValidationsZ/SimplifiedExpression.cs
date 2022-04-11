using EiopaConstants;
using GeneralUtilsNs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace Validations
{

    public class PartialExpression
    {
        public string Letter { get; set; }
        public string Expression { get; set; }
        public bool IsValid { get; }
    }

    public class SimplifiedExpression
    {
        public string Expression { get; set; }
        public string SymbolExpression { get; set; } = "";
        public Dictionary<string, ObjTerm> ObjTerms { get; set; } = new();
        public bool IsValid { get; set; }
        public List<PartialExpression> PartialExpressions { get; set; } = new();
        private SimplifiedExpression() { }

        public static SimplifiedExpression CreateExpression(string expression)
        {
            var se = new SimplifiedExpression(expression);
            se.CreateTerms();
            return se;
        }
        private SimplifiedExpression(string expression)
        {
            Expression = expression;            
        }


        public void CreateTerms()
        {
            if (string.IsNullOrWhiteSpace(Expression))
                return;

            var newFormula = Expression;

            var terms = Expression.Split(new string[] { "&&", "||" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var count = 0;
            foreach (var term in terms)
            {
                PartialExpressions.Add(new PartialExpression() {Letter=$"VV{count}", Expression = term.Trim() });
                count += 1;
            }

            SymbolExpression = PartialExpressions
                .Aggregate(newFormula, (currValue, termExpression) => currValue.Replace(termExpression.Expression, $" {termExpression.Letter} "))
                .Trim();
            

        }
        private bool AssertExperssion(List<RuleTerm> ruleTerms)
        {
            ObjTerms = CreateObjectTerms(ruleTerms);
            foreach(var partialExpression in PartialExpressions)
            {
                var peLetters = GeneralUtils.GetRegexListOfMatchesWithCase(@"([XZT]\d{1,2})", partialExpression.Letter).Distinct();// get X0,X1,Z0,... from expression and then get only the terms corresponding to these
                var teObjTerms = ObjTerms.Where(obj => peLetters.Contains(obj.Key));

            }
            return true;
        }
        
        public bool AssertExpression(List<RuleTerm> ruleTerms)
        {
            //var isAllDouble = ObjTerms.All(obj => obj.Value.obj?.GetType() == typeof(double));
            ObjTerms = CreateObjectTerms(ruleTerms);
            return false;
        }


        private  Dictionary<string, ObjTerm> CreateObjectTerms( List<RuleTerm> ruleTerms)
        {
            Dictionary<string, ObjTerm> xobjTerms = new();
            //var letters = GeneralUtils.GetRegexListOfMatchesWithCase(@"([XZT]\d{1,2})", formula).Distinct();// get X0,X1,Z0,... to avoid x0 
            
            //var xxTerms = terms.Where(rt => letters.Contains(rt.Letter)).ToList();

            foreach (var term in ruleTerms)
            {                
                ObjTerm objTerm;
                if (term.IsMissing)
                {
                    objTerm = new ObjTerm
                    { 
                        obj = term.DataTypeOfTerm switch
                        {
                            DataTypeMajorUU.BooleanDtm => false,
                            DataTypeMajorUU.StringDtm => "",
                            DataTypeMajorUU.DateDtm => new DateTime(2000, 1, 1),
                            DataTypeMajorUU.NumericDtm => Convert.ToDouble(0.00),
                            _ => term.TextValue,
                        },
                        decimals = term.NumberOfDecimals,
                    };
                }
                else
                {
                    objTerm = new ObjTerm
                    {
                        obj = term.DataTypeOfTerm switch
                        {
                            DataTypeMajorUU.BooleanDtm => term.BooleanValue,
                            DataTypeMajorUU.StringDtm => term.TextValue,
                            DataTypeMajorUU.DateDtm => term.DateValue,
                            //DataTypeMajorUU.NumericDtm => Math.Round( Convert.ToDouble(term.DecimalValue),5),
                            DataTypeMajorUU.NumericDtm => Convert.ToDouble(Math.Truncate(term.DecimalValue * 1000) / 1000), // truncate to 3 decimals
                            _ => term.TextValue,
                        },
                        decimals = term.NumberOfDecimals,
                    };

                }
                
                if (!xobjTerms.ContainsKey(term.Letter))
                {
                    xobjTerms.Add(term.Letter, objTerm);
                }

            }
            return xobjTerms;
        }


    }
}
