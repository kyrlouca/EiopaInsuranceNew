using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityClasses
{
    public class MappingSignatures
    {
        
        public int MappingSignatureId { get; set; }
        public int InstanceId { get; set; }
        public int TableId { get; set; }
        public string Signature { get; set; }
        public string RowCol { get; set; }
        public bool IsOpenTable { get; set; }
        private MappingSignatures() { }
        
        public MappingSignatures( int instanceId, int tableId, string signature, string rowCol, bool isOpenTable)
        {            
            InstanceId=instanceId;
            TableId = tableId;
            Signature = signature;
            RowCol = rowCol;
            IsOpenTable = isOpenTable;
        }

    }
}
