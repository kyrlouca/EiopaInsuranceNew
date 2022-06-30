using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityClassesZ
{
    public class ExcelSheetChild
    {
        public int SheetChildId { get; set; }
        public string SheetCode { get; set; }
        public int SheetParentId { get; set; }
        public string Description { get; set; }
    }
}
