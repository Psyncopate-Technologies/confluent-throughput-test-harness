// ────────────────────────────────────────────────────────────────────
// AvroSmallDataFactory.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Builds an Avro GenericRecord populated with sample data
//           for the small (25-field) freight schema. Used to produce
//           test messages for the Avro small producer benchmark.
// ────────────────────────────────────────────────────────────────────

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

        record.Add("Id_", 100001);
        record.Add("LoadDate", now);
        record.Add("PONumber", 50001);
        record.Add("CarrierId", 2001);
        record.Add("CarrierName", "FastFreight Logistics LLC");
        record.Add("DriverId", 3001);
        record.Add("DriverName", "John Smith");
        record.Add("DeliveryDate", now);
        record.Add("Remarks", "Standard delivery - no special handling required. Customer has dock access available 7AM-5PM weekdays.");
        record.Add("Dispatched_YN", true);
        record.Add("Covered_YN", true);
        record.Add("PayTruckAmount", new AvroDecimal(2500.0000m));
        record.Add("TrailerType", "Refrigerated");
        record.Add("CustomerName", "Acme Food Distribution Inc.");
        record.Add("CustomerPO", "PO-2024-78543");
        record.Add("DateChanged", now);
        record.Add("UniqueId", guid);
        record.Add("DateCreated", now);
        record.Add("ExpenseTotal", new AvroDecimal(3250.0000m));
        record.Add("ChargesTotal", new AvroDecimal(4100.0000m));
        record.Add("Weight", 42000);
        record.Add("RoadMiles", 487);
        record.Add("rowguid", Guid.NewGuid().ToString("D").Substring(0, 36));
        record.Add("__cdc_integ_key", "100001");
        record.Add("__cdc_op_val", 1);
        record.Add("__test_seq", 0);
        record.Add("__test_ts", "");

        return record;
    }

    public void SetMessageHeader(GenericRecord record, int sequenceNumber, string timestamp)
    {
        record.Add("__test_seq", sequenceNumber);
        record.Add("__test_ts", timestamp);
    }
}
