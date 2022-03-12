using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityClasses
{




    public class Context
    {
        public int InstanceId { get; set; }
        public int ContextId { get; set; }
        public string ContextXbrlId { get; set; }
        public string Signature { get; private set; }
        public int TableId { get; set; }
        public List<string> ContextLinesF1 = new();

        public Context(int instanceId, string contextXbrlId, string signature, int tableId)
        {
            InstanceId = instanceId;
            ContextXbrlId = contextXbrlId;
            Signature = signature;
            TableId = tableId;
        }
        private Context()
        {

        }
        public string  BuildSignature()
        {
            Signature = string.Join("|", ContextLinesF1);            
            return Signature;
        }

    }
}
