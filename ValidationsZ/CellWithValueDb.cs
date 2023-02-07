using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HelperInsuranceFunctions;
using Validations;
using ConfigurationNs;
using Shared.Services;

namespace Validations
{
    public class CellWithValueDb
    {
        //input {S.23.01.01.01,r0540,c0040} and creates an object with TermObject and DbValue
        //The TermObject has the table code, row, col and the DbValue has the value from the database
        public string Text { get; internal set; }
        public TermObject CellObject { get; set; }
        public DbValue DbValue { get; internal set; }
        readonly IConfigObject configObj;
        readonly int DocId;

        CellWithValueDb() { }
        CellWithValueDb(IConfigObject configObject, int documentId, string text)
        {
            configObj = configObject;
            DocId = documentId;
            Text = text;
        }
        void GetTheValue()
        {
            CellObject = TermObject.Parse(Text);
            DbValue = DocumentValidator.GetCellValueFromDbNew(configObj, DocId, CellObject.TableCode, CellObject.Row, CellObject.Col);
        }
        public static CellWithValueDb GetValue(IConfigObject configObject, int documentId, string text)
        {
            var obj = new CellWithValueDb(configObject, documentId, text);
            obj.GetTheValue();
            return obj;
        }
    }
}
