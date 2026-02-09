// ────────────────────────────────────────────────────────────────────
// AvroSmallSpecificDataFactory.cs
// Created:  2026-02-09
// Author:   Ayu Admassu
// Purpose:  Builds a FreightSmallSpecific (ISpecificRecord) populated
//           with sample data for the small (27-field) freight schema.
//           Used to produce test messages for the Avro small
//           SpecificRecord producer benchmark.
// ────────────────────────────────────────────────────────────────────

using Avro;
using ConfluentThroughputTestHarness.Models.AvroSpecific;

namespace ConfluentThroughputTestHarness.DataFactories;

public class AvroSmallSpecificDataFactory : ITestDataFactory<FreightSmallSpecific>
{
    public FreightSmallSpecific CreateRecord()
    {
        var now = DateTime.UtcNow;
        var guid = Guid.NewGuid().ToString();

        return new FreightSmallSpecific
        {
            Id_ = 100001,
            LoadDate = now,
            PONumber = 50001,
            CarrierId = 2001,
            CarrierName = "FastFreight Logistics LLC",
            DriverId = 3001,
            DriverName = "John Smith",
            DeliveryDate = now,
            Remarks = "Standard delivery - no special handling required. Customer has dock access available 7AM-5PM weekdays.",
            Dispatched_YN = true,
            Covered_YN = true,
            PayTruckAmount = new AvroDecimal(2500.0000m),
            TrailerType = "Refrigerated",
            CustomerName = "Acme Food Distribution Inc.",
            CustomerPO = "PO-2024-78543",
            DateChanged = now,
            UniqueId = guid,
            DateCreated = now,
            ExpenseTotal = new AvroDecimal(3250.0000m),
            ChargesTotal = new AvroDecimal(4100.0000m),
            Weight = 42000,
            RoadMiles = 487,
            rowguid = Guid.NewGuid().ToString("D")[..36],
            __cdc_integ_key = "100001",
            __cdc_op_val = 1,
            __test_seq = 0,
            __test_ts = "",
        };
    }

    public void SetMessageHeader(FreightSmallSpecific record, int sequenceNumber, string timestamp)
    {
        record.__test_seq = sequenceNumber;
        record.__test_ts = timestamp;
    }
}
