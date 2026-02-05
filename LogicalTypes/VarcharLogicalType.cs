using Avro;
using Avro.Util;

namespace ConfluentThroughputTestHarness.LogicalTypes;

public class VarcharLogicalType : LogicalType
{
    public VarcharLogicalType() : base("varchar") { }

    public override object ConvertToBaseValue(object logicalValue, LogicalSchema schema) => logicalValue;
    public override object ConvertToLogicalValue(object baseValue, LogicalSchema schema) => baseValue;
    public override Type GetCSharpType(bool nullible) => typeof(string);
    public override bool IsInstanceOfLogicalType(object logicalValue) => logicalValue is string;

    public override void ValidateSchema(LogicalSchema schema)
    {
        if (Schema.Type.String != schema.BaseSchema.Tag)
            throw new AvroTypeException("'varchar' can only be used with an underlying string type");
    }
}
