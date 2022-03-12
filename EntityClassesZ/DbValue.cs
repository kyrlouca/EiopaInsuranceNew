using EiopaConstants;
namespace EntityClasses
{
    public class DbValue
    {
        public int FactId { get; }
        public string TextValue { get; }
        public decimal DecimalValue { get; }
        public bool BoolValue { get; }
        public DataTypeMajorUU DataTypeEnumMajor { get; }
        public string MetricDataType { get; }
        public bool IsMissing { get; }        

        
        public DbValue(int factId, string textValue, decimal decimalValue, bool boolValue, string metricDataType, DataTypeMajorUU dataTypeEnumMajor, bool isMissing)
        {
            FactId = factId;
            TextValue = textValue;
            DecimalValue = decimalValue;
            BoolValue = boolValue;
            MetricDataType = metricDataType;
            DataTypeEnumMajor = dataTypeEnumMajor;
            IsMissing = isMissing;
        }

    }
}
