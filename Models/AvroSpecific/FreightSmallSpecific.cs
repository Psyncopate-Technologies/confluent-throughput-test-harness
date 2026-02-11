// ────────────────────────────────────────────────────────────────────
// FreightSmallSpecific.cs
// Created:  2026-02-09
// Author:   Ayu Admassu
// Purpose:  Hand-written ISpecificRecord implementation for the small
//           (27-field) freight CDC schema. Required because avrogen
//           cannot handle the custom varchar/char logical types.
//           Field order in Get/Put matches the .avsc field order exactly.
// ────────────────────────────────────────────────────────────────────

using Avro;
using Avro.Specific;
using ConfluentThroughputTestHarness.Config;

namespace ConfluentThroughputTestHarness.Models.AvroSpecific;

public class FreightSmallSpecific : ISpecificRecord
{
    private static readonly Lazy<RecordSchema> _schemaLazy = new(() =>
    {
        var json = SchemaRegistryCache.GetSchemaJsonFromCache("test-avro-small-value");
        return (RecordSchema)Schema.Parse(json);
    });

    public static RecordSchema _SCHEMA => _schemaLazy.Value;
    public Schema Schema => _SCHEMA;

    // ── Fields (0–26, matching .avsc order) ─────────────────────────
    public int? Id_ { get; set; }                          //  0
    public DateTime? LoadDate { get; set; }                 //  1
    public int PONumber { get; set; }                       //  2
    public int? CarrierId { get; set; }                     //  3
    public string? CarrierName { get; set; }                //  4
    public int? DriverId { get; set; }                      //  5
    public string? DriverName { get; set; }                 //  6
    public DateTime? DeliveryDate { get; set; }             //  7
    public string? Remarks { get; set; }                    //  8
    public bool? Dispatched_YN { get; set; }                //  9
    public bool? Covered_YN { get; set; }                   // 10
    public AvroDecimal? PayTruckAmount { get; set; }        // 11
    public string? TrailerType { get; set; }                // 12
    public string? CustomerName { get; set; }               // 13
    public string? CustomerPO { get; set; }                 // 14
    public DateTime? DateChanged { get; set; }              // 15
    public string? UniqueId { get; set; }                   // 16
    public DateTime? DateCreated { get; set; }              // 17
    public AvroDecimal? ExpenseTotal { get; set; }          // 18
    public AvroDecimal? ChargesTotal { get; set; }          // 19
    public int? Weight { get; set; }                        // 20
    public int? RoadMiles { get; set; }                     // 21
    public string rowguid { get; set; } = string.Empty;     // 22
    public string __cdc_integ_key { get; set; } = string.Empty; // 23
    public int __cdc_op_val { get; set; }                   // 24
    public int __test_seq { get; set; }                     // 25
    public string __test_ts { get; set; } = string.Empty;   // 26

    public object Get(int fieldPos)
    {
        return fieldPos switch
        {
            0  => Id_!,
            1  => LoadDate!,
            2  => PONumber,
            3  => CarrierId!,
            4  => CarrierName!,
            5  => DriverId!,
            6  => DriverName!,
            7  => DeliveryDate!,
            8  => Remarks!,
            9  => Dispatched_YN!,
            10 => Covered_YN!,
            11 => PayTruckAmount!,
            12 => TrailerType!,
            13 => CustomerName!,
            14 => CustomerPO!,
            15 => DateChanged!,
            16 => UniqueId!,
            17 => DateCreated!,
            18 => ExpenseTotal!,
            19 => ChargesTotal!,
            20 => Weight!,
            21 => RoadMiles!,
            22 => rowguid,
            23 => __cdc_integ_key,
            24 => __cdc_op_val,
            25 => __test_seq,
            26 => __test_ts,
            _  => throw new AvroRuntimeException($"Bad index {fieldPos} in Get()")
        };
    }

    public void Put(int fieldPos, object fieldValue)
    {
        switch (fieldPos)
        {
            case 0:  Id_ = (int?)fieldValue; break;
            case 1:  LoadDate = (DateTime?)fieldValue; break;
            case 2:  PONumber = (int)fieldValue; break;
            case 3:  CarrierId = (int?)fieldValue; break;
            case 4:  CarrierName = (string?)fieldValue; break;
            case 5:  DriverId = (int?)fieldValue; break;
            case 6:  DriverName = (string?)fieldValue; break;
            case 7:  DeliveryDate = (DateTime?)fieldValue; break;
            case 8:  Remarks = (string?)fieldValue; break;
            case 9:  Dispatched_YN = (bool?)fieldValue; break;
            case 10: Covered_YN = (bool?)fieldValue; break;
            case 11: PayTruckAmount = (AvroDecimal?)fieldValue; break;
            case 12: TrailerType = (string?)fieldValue; break;
            case 13: CustomerName = (string?)fieldValue; break;
            case 14: CustomerPO = (string?)fieldValue; break;
            case 15: DateChanged = (DateTime?)fieldValue; break;
            case 16: UniqueId = (string?)fieldValue; break;
            case 17: DateCreated = (DateTime?)fieldValue; break;
            case 18: ExpenseTotal = (AvroDecimal?)fieldValue; break;
            case 19: ChargesTotal = (AvroDecimal?)fieldValue; break;
            case 20: Weight = (int?)fieldValue; break;
            case 21: RoadMiles = (int?)fieldValue; break;
            case 22: rowguid = (string)fieldValue; break;
            case 23: __cdc_integ_key = (string)fieldValue; break;
            case 24: __cdc_op_val = (int)fieldValue; break;
            case 25: __test_seq = (int)fieldValue; break;
            case 26: __test_ts = (string)fieldValue; break;
            default: throw new AvroRuntimeException($"Bad index {fieldPos} in Put()");
        }
    }
}
