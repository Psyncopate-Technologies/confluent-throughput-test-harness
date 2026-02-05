// ────────────────────────────────────────────────────────────────────
// VarcharLogicalType.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Custom Avro logical type handler for the "varchar" logical
//           type used in the freight CDC schema. Maps varchar fields
//           to .NET strings during serialization and deserialization.
// ────────────────────────────────────────────────────────────────────

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
