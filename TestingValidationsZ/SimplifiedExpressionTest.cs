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
            var simplified = SimplifiedExpression.Process(1,null,str,true,true);
            simplified.SymbolExpressionFinal.Equals("VV00 && VV01");
            simplified.TermExpressions.Count.Equals(2);
            simplified.TermExpressions[0].TermExpressionStr.Equals("X0 >= 0.25*X2");
            simplified.TermExpressions[1].TermExpressionStr.Equals("X0 <= 0.45*X2");

             str = @"X0 >= 0.25*X2 && X0 <= 0.45*X2 ||  X4";
            simplified = SimplifiedExpression.Process(1,null,str,true,true);
            simplified.SymbolExpressionFinal.Equals("VV00 && VV01 || VV02");
            simplified.TermExpressions.Count.Equals(3);
            simplified.TermExpressions[0].TermExpressionStr.Equals("X0 >= 0.25*X2");
            simplified.TermExpressions[1].TermExpressionStr.Equals("X0 <= 0.45*X2");
            simplified.TermExpressions[2].TermExpressionStr.Equals("X4");


            str = @"X0 >= 0.25*X2  ";
            simplified = SimplifiedExpression.Process(1, null, str,true,true);
            simplified.SymbolExpressionFinal.Equals("VV00");
            simplified.TermExpressions.Count.Equals(1);
            simplified.TermExpressions[0].TermExpressionStr.Equals("X0 >= 0.25*X2");

            str = @"(X0 == X2)";
            simplified = SimplifiedExpression.Process(1, null, str,true,true);
            simplified.SymbolExpressionFinal.Equals("VV00");
            simplified.TermExpressions.Count.Equals(1);
            simplified.TermExpressions[0].TermExpressionStr.Equals("X0 == X2");


            str = @" ";
            simplified = SimplifiedExpression.Process(1, null, str,true,true);
            simplified.SymbolExpressionFinal.Equals("");
            simplified.TermExpressions.Count.Equals(0);

            string str2=null;
            simplified = SimplifiedExpression.Process(1,null,str2,true,true);
            simplified.SymbolExpressionFinal.Equals("");
            simplified.TermExpressions.Count.Equals(0);

        }

        [Fact]
        public void ValidateSimplifiedRecurse()
        {
            //function terms NOT normal terms
            //Do not test nested Terms here-- use other test

            var str = @"!(Z0) && (!(Z1) || !(Z2) || !(Z3) || !(Z4))";
            var simplified = SimplifiedExpression.Process(1, null, str,true,true);
            simplified.SymbolExpressionFinal.Equals("VV00 && VV01");
            simplified.TermExpressions.Count.Equals(2);
            //simplified.TermExpressions[0].TermExpressionStr.Equals("VV0");
            //simplified.PartialSimplifiedExpressions[0]
            //simplified.TermExpressions[1].TermExpressionStr.Equals("X0 <= 0.45*X2");

            

        }


    }
}
