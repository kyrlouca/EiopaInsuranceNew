using System;
using System.Collections.Generic;
using System.Text;

namespace EntityClasses
{
    public class MAPPING
    {
        public decimal TABLE_VERSION_ID { get; set; }
        public string DYN_TABLE_NAME { get; set; }
        public string DYN_TAB_COLUMN_NAME { get; set; }
        public string DIM_CODE { get; set; }
        public string DOM_CODE { get; set; }
        public string MEM_CODE { get; set; }
        public string ORIGIN { get; set; }
        public int REQUIRED_MAPPINGS { get; set; }
        public int PAGE_COLUMNS_NUMBER { get; set; }
        public string DATA_TYPE { get; set; }
        public decimal IS_PAGE_COLUMN_KEY { get; set; }
        public decimal IS_DEFAULT { get; set; }
        public decimal IS_IN_TABLE { get; set; }
    }
}
