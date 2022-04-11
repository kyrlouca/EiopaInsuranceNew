using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Validations
{
    public class SimpleExpression
    {
        public string Expression { get; set; }
        public Dictionary<string,RuleTerm> RuleTerms { get; set; }
        public bool IsValid { get; set; }
        public bool AssertExpression()
        {
            //var isAllDouble = ObjTerms.All(obj => obj.Value.obj?.GetType() == typeof(double));
            return false;
        }
    }
}
