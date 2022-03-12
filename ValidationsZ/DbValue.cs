﻿
using EiopaConstants;
using System;

namespace Validations
{
    //public enum ValueTypes { String, Numeric, Boolean }
    public class DbValue
    {
        public int FactId { get; }
        public string TextValue { get;  }
        public decimal DecimalValue { get;  }
        public DateTime DateValue { get; }
        public bool BoolValue { get;   }
        public DataTypeMajorUU DataTypeEnumMajorUU  { get; set; }
        public bool IsMissing { get; }

        private  DbValue() { }
        public DbValue(int factId, string textValue, decimal decimalValue, DateTime dateValue, bool boolValue,  DataTypeMajorUU dataTypeEnumMajor, bool isMissing)
        {
            FactId = factId;
            TextValue = textValue;
            DecimalValue = decimalValue;
            DateValue = dateValue;
            BoolValue = boolValue;
            //MetricDataTypex = metricDataType;
            DataTypeEnumMajorUU = dataTypeEnumMajor;
            IsMissing = isMissing;
            //NewDataTypeUsex= newDataTypeUse;
        }

    }
}
