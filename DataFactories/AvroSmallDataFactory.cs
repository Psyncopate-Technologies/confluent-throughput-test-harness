using Avro;
using Avro.Generic;

namespace ConfluentThroughputTestHarness.DataFactories;

public class AvroSmallDataFactory : ITestDataFactory<GenericRecord>
{
    private readonly RecordSchema _schema;

    public AvroSmallDataFactory(RecordSchema schema)
    {
        _schema = schema;
    }

    public GenericRecord CreateRecord()
    {
        var record = new GenericRecord(_schema);
        var now = DateTime.UtcNow;
        var guid = Guid.NewGuid().ToString();

        // UUID fields - uuid logicalType maps to Guid
        var guidVal = Guid.NewGuid();
        record.Add("Guid1", guidVal);
        record.Add("Guid1N", guidVal);
        record.Add("Guid1NV", guidVal);

        // Boolean fields
        record.Add("Bool1", true);
        record.Add("Bool1N", true);
        record.Add("Bool1NV", false);

        // String fields
        record.Add("String1", "TestStringValue");
        record.Add("String1N", "NullableStringWithValue");
        record.Add("String1NV", "AnotherString");

        // Int fields
        record.Add("Int1", 42);
        record.Add("Int1N", 100);
        record.Add("Int1NV", 200);

        // Long fields
        record.Add("Long1", 123456789L);
        record.Add("Long1N", 987654321L);
        record.Add("Long1NV", 555555555L);

        // Float fields
        record.Add("Float1", 3.14f);
        record.Add("Float1N", 2.718f);
        record.Add("Float1NV", 1.414f);

        // Double fields
        record.Add("Double1", 3.14159265358979);
        record.Add("Double1N", 2.71828182845905);
        record.Add("Double1NV", 1.41421356237310);

        // Decimal fields (stored as AvroDecimal for logicalType decimal)
        record.Add("Decimal1", new AvroDecimal(1234567.8m));
        record.Add("Decimal1N", new AvroDecimal(9876543.2m));
        record.Add("Decimal1NV", new AvroDecimal(1111111.1m));

        record.Add("Decimal2", new AvroDecimal(123456.78m));
        record.Add("Decimal2N", new AvroDecimal(987654.32m));
        record.Add("Decimal2NV", new AvroDecimal(111111.11m));

        record.Add("Decimal4", new AvroDecimal(1234.5678m));
        record.Add("Decimal4N", new AvroDecimal(9876.5432m));
        record.Add("Decimal4NV", new AvroDecimal(1111.1111m));

        // Timestamp-millis fields
        record.Add("DTUnspecified", now);
        record.Add("DTUnspecifiedN", now);
        record.Add("DTUnspecifiedNV", now);

        record.Add("DTUTC", now);
        record.Add("DTUTCN", now);
        record.Add("DTUTCNV", now);

        record.Add("DTUTCMillis", now);
        record.Add("DTUTCMicros", now);

        record.Add("DTLocal", now);
        record.Add("DTLocalN", now);
        record.Add("DTLocalNV", now);

        record.Add("DTLocalMillis", now);
        record.Add("DTLocalMicros", now);

        record.Add("DTOUTC1", now);
        record.Add("DTOUTC1N", now);
        record.Add("DTOUTC1NV", now);

        record.Add("DTOLocal1", now);
        record.Add("DTOLocal1N", now);
        record.Add("DTOLocal1NV", now);

        // Enum fields - use GenericEnum for Avro enum type
        var enumSchema = (EnumSchema)_schema.Fields.First(f => f.Name == "OrderStatus1").Schema;
        record.Add("OrderStatus1", new GenericEnum(enumSchema, "Shipped"));
        record.Add("OrderStatus1N", new GenericEnum(enumSchema, "Pending"));
        record.Add("OrderStatus1NV", new GenericEnum(enumSchema, "Delivered"));

        return record;
    }
}
