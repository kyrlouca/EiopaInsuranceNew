using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityClasses
{
    public class RowSignatureToDelete
    {        
        public int TemplateSheetId { get; set; }       
        public string Signature { get; set; }
        public int RowNumber { get; set; }
        public int TableCode { get; set; }
    }
}
