using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Validations;

namespace TestingValidationsZ
{
    
    public class TestSimplified
    {
        [Fact]
        public void ValidateSimplified()
        {
            //function terms NOT normal terms
            //Do not test nested Terms here-- use other test

            var str = @"X0 >= 0.25*X2 && X0 <= 0.45*X2 ";
            var simplified = SimplifiedExpression.CreateExpression(str);
            simplified.Expression = "VV0 && VV1";
            simplified.PartialExpressions.Count.Equals(2);
            simplified.PartialExpressions[0].Expression.Equals("X0 >= 0.25*X2");
            simplified.PartialExpressions[1].Expression.Equals("X0 <= 0.45*X2");

             str = @"X0 >= 0.25*X2 && X0 <= 0.45*X2 ||  X4";
            simplified = SimplifiedExpression.CreateExpression(str);
            simplified.Expression = "VV0 && VV1 || VV2";
            simplified.PartialExpressions.Count.Equals(3);
            simplified.PartialExpressions[0].Expression.Equals("X0 >= 0.25*X2");
            simplified.PartialExpressions[1].Expression.Equals("X0 <= 0.45*X2");
            simplified.PartialExpressions[2].Expression.Equals("X4");


            str = @"X0 >= 0.25*X2  ";
            simplified = SimplifiedExpression.CreateExpression(str);
            simplified.Expression = "VV0 ";
            simplified.PartialExpressions.Count.Equals(1);
            simplified.PartialExpressions[0].Expression.Equals("X0 >= 0.25*X2");

            str = @"(X0 == X2)";
            simplified = SimplifiedExpression.CreateExpression(str);
            simplified.Expression = "VV0 ";
            simplified.PartialExpressions.Count.Equals(1);
            simplified.PartialExpressions[0].Expression.Equals("X0 == X2");


            str = @" ";
            simplified = SimplifiedExpression.CreateExpression(str);
            simplified.Expression = "";
            simplified.PartialExpressions.Count.Equals(0);

            string str2=null;
            simplified = SimplifiedExpression.CreateExpression(str2);
            simplified.Expression = "";
            simplified.PartialExpressions.Count.Equals(0);

        }

    }
}
