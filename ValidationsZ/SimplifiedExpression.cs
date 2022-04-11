using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace Validations
{

    public class TermExpression
    {
        public string Letter { get; set; }
        public string Expression { get; set; }
        public bool IsValid { get; }
    }

    public class SimplifiedExpression
    {
        public string Expression { get; set; }
        public string SymbolExpression { get; set; } = "";
        public Dictionary<string, RuleTerm> RuleTerms { get; set; } = new();
        public bool IsValid { get; set; }
        public List<TermExpression> TermExpressions { get; set; } = new();
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
                TermExpressions.Add(new TermExpression() {Letter=$"VV{count}", Expression = term.Trim() });
                count += 1;
            }

            SymbolExpression = TermExpressions
                .Aggregate(newFormula, (currValue, termExpression) => currValue.Replace(termExpression.Expression, $" {termExpression.Letter} "))
                .Trim();
            

        }
        public bool AssertExpression()
        {
            //var isAllDouble = ObjTerms.All(obj => obj.Value.obj?.GetType() == typeof(double));
            return false;
        }
    }
}
