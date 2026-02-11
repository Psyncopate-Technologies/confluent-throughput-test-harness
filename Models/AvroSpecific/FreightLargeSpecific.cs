// ────────────────────────────────────────────────────────────────────
// FreightLargeSpecific.cs
// Created:  2026-02-09
// Author:   Ayu Admassu
// Purpose:  Hand-written ISpecificRecord implementation for the large
//           (130-field) freight CDC tblloads schema. Required because
//           avrogen cannot handle the custom varchar/char logical types.
//           Field order in Get/Put matches the .avsc field order exactly.
// ────────────────────────────────────────────────────────────────────

using Avro;
using Avro.Specific;
using ConfluentThroughputTestHarness.Config;

namespace ConfluentThroughputTestHarness.Models.AvroSpecific;

public class FreightLargeSpecific : ISpecificRecord
{
    private static readonly Lazy<RecordSchema> _schemaLazy = new(() =>
    {
        var json = SchemaRegistryCache.GetSchemaJsonFromCache("test-avro-large-value");
        return (RecordSchema)Schema.Parse(json);
    });

    public static RecordSchema _SCHEMA => _schemaLazy.Value;
    public Schema Schema => _SCHEMA;

    // ── Fields (0–129, matching .avsc order) ────────────────────────
    public int? Id_ { get; set; }                          //   0
    public DateTime? LoadDate { get; set; }                 //   1
    public DateTime? SystemDate { get; set; }               //   2
    public int PONumber { get; set; }                       //   3
    public int? LoadSplitNumber { get; set; }               //   4
    public int? CarrierId { get; set; }                     //   5
    public string? CarrierName { get; set; }                //   6
    public int? DriverId { get; set; }                      //   7
    public string? DriverName { get; set; }                 //   8
    public DateTime? DeliveryDate { get; set; }             //   9
    public string? Remarks { get; set; }                    //  10
    public int? SalesPersonId { get; set; }                 //  11
    public string? SalesPersonName { get; set; }            //  12
    public int? SavedBySalesPersonId { get; set; }          //  13
    public string? SavedBySalesPersonName { get; set; }     //  14
    public string? CheckIn_YN { get; set; }                 //  15
    public bool? Dispatched_YN { get; set; }                //  16
    public bool? Covered_YN { get; set; }                   //  17
    public string? Temperature { get; set; }                //  18
    public string? TripMiles { get; set; }                  //  19
    public bool? Inactive_YN { get; set; }                  //  20
    public bool? PayCarrier_YN { get; set; }                //  21
    public bool? PayImportDate_YN { get; set; }             //  22
    public bool? BillAllCustomers_YN { get; set; }          //  23
    public bool? BillCustomerImportData_YN { get; set; }    //  24
    public AvroDecimal? PayTruckAmount { get; set; }        //  25
    public string? TrailerType { get; set; }                //  26
    public int? TrailerId { get; set; }                     //  27
    public string? TrailerSize { get; set; }                //  28
    public int? TrailerSizeId { get; set; }                 //  29
    public bool? UrgentMessage { get; set; }                //  30
    public bool? PalletExchange { get; set; }               //  31
    public bool? ReceiverUnloading { get; set; }            //  32
    public int? UnloadingCharges { get; set; }              //  33
    public bool? DeliveredOnTime { get; set; }              //  34
    public bool? NeedsAttention { get; set; }               //  35
    public bool? Void { get; set; }                         //  36
    public bool? Locked { get; set; }                       //  37
    public bool? Claim { get; set; }                        //  38
    public string? TrailerComments { get; set; }            //  39
    public string? CustomerName { get; set; }               //  40
    public string? CustomerPO { get; set; }                 //  41
    public int? CheckCallList { get; set; }                 //  42
    public int? LoadStatusInfo { get; set; }                //  43
    public int? DataChanged { get; set; }                   //  44
    public DateTime? DateChanged { get; set; }              //  45
    public bool? SplitLoad { get; set; }                    //  46
    public string? Consignees { get; set; }                 //  47
    public string? DriversCellPhone { get; set; }           //  48
    public DateTime? DateMarkedDelivered { get; set; }      //  49
    public int? QuickBooksStatus { get; set; }              //  50
    public string? QuickBooksStatusReason { get; set; }     //  51
    public bool? NoSundayCC { get; set; }                   //  52
    public DateTime? DateSentToQB { get; set; }             //  53
    public string? UniqueId { get; set; }                   //  54
    public DateTime? DateCreated { get; set; }              //  55
    public string? CreatedBy { get; set; }                  //  56
    public string? Computer { get; set; }                   //  57
    public string? Dispatcher { get; set; }                 //  58
    public int? Pallets { get; set; }                       //  59
    public DateTime? TimeToCheckBy { get; set; }            //  60
    public string? NetworkLogon { get; set; }               //  61
    public int? AssistantId { get; set; }                   //  62
    public int? RoadMiles { get; set; }                     //  63
    public AvroDecimal? ExpenseTotal { get; set; }          //  64
    public AvroDecimal? ChargesTotal { get; set; }          //  65
    public AvroDecimal? CustomerBillTotal { get; set; }     //  66
    public int? Weight { get; set; }                        //  67
    public AvroDecimal? ChargeCustomerUnloadingTotal { get; set; } //  68
    public AvroDecimal? PayCarrierUnloadingTotal { get; set; } //  69
    public int? CurrentCarrierStatus { get; set; }          //  70
    public string? VoidReason { get; set; }                 //  71
    public bool? DeliveredLoadChange { get; set; }          //  72
    public int? InternalDatSearch { get; set; }             //  73
    public bool? DriverReload { get; set; }                 //  74
    public AvroDecimal? MinPayTruck { get; set; }           //  75
    public AvroDecimal? MaxPayTruck { get; set; }           //  76
    public DateTime? DateTurnedIn { get; set; }             //  77
    public string? CarrierUniqueId { get; set; }            //  78
    public string rowguid { get; set; } = string.Empty;     //  79
    public int? NightCoverLoad { get; set; }                //  80
    public bool? SendToCRS { get; set; }                    //  81
    public int? NeedAppointment { get; set; }               //  82
    public int? OKForComcheck { get; set; }                 //  83
    public string? ComcheckAmount { get; set; }             //  84
    public string? CSATask { get; set; }                    //  85
    public DateTime? CustomerRCDate { get; set; }           //  86
    public string? DriverUniqueId { get; set; }             //  87
    public string? Lane { get; set; }                       //  88
    public int? CSAUserID { get; set; }                     //  89
    public int? RecordStatus { get; set; }                  //  90
    public int? LabelPrinted { get; set; }                  //  91
    public int? GPStatus { get; set; }                      //  92
    public int? Location { get; set; }                      //  93
    public int? IDATRadius { get; set; }                    //  94
    public int? LoadCommodityType { get; set; }             //  95
    public int? PickupCount { get; set; }                   //  96
    public int? DropCount { get; set; }                     //  97
    public int? PracticalMiles { get; set; }                //  98
    public int? ShortMiles { get; set; }                    //  99
    public string? DispatcherUniqueID { get; set; }         // 100
    public int? CaseCount { get; set; }                     // 101
    public int? ServiceType { get; set; }                   // 102
    public int? HazmatIndicator { get; set; }               // 103
    public int? LoadValue { get; set; }                     // 104
    public int? TemperatureCategory { get; set; }           // 105
    public string? LoadCommodityOther { get; set; }         // 106
    public int? LoadBoardUserId { get; set; }               // 107
    public int? RateType { get; set; }                      // 108
    public bool? IsCustomerChargingLateFee { get; set; }    // 109
    public AvroDecimal? ExactLoadValue { get; set; }        // 110
    public int? CustomerId { get; set; }                    // 111
    public int? Driver2Id { get; set; }                     // 112
    public string? Driver2Name { get; set; }                // 113
    public string? Driver2CellPhone { get; set; }           // 114
    public string? Driver2UniqueId { get; set; }            // 115
    public bool? TeamCover { get; set; }                    // 116
    public bool? EmergencyCover { get; set; }               // 117
    public DateTime? DateSentToAccounting { get; set; }     // 118
    public int? SentToAccountingByUserId { get; set; }      // 119
    public DateTime? ReeferCheckInDate { get; set; }        // 120
    public string? ContainerNumber { get; set; }            // 121
    public string? TrailerNumber { get; set; }              // 122
    public string __cdc_cap_tstamp { get; set; } = string.Empty;    // 123
    public string __cdc_changedby_user { get; set; } = string.Empty; // 124
    public string __cdc_integ_key { get; set; } = string.Empty;     // 125
    public string __cdc_integ_tstamp { get; set; } = string.Empty;  // 126
    public int __cdc_op_val { get; set; }                   // 127
    public int __test_seq { get; set; }                     // 128
    public string __test_ts { get; set; } = string.Empty;   // 129

    public object Get(int fieldPos)
    {
        return fieldPos switch
        {
            0   => Id_!,
            1   => LoadDate!,
            2   => SystemDate!,
            3   => PONumber,
            4   => LoadSplitNumber!,
            5   => CarrierId!,
            6   => CarrierName!,
            7   => DriverId!,
            8   => DriverName!,
            9   => DeliveryDate!,
            10  => Remarks!,
            11  => SalesPersonId!,
            12  => SalesPersonName!,
            13  => SavedBySalesPersonId!,
            14  => SavedBySalesPersonName!,
            15  => CheckIn_YN!,
            16  => Dispatched_YN!,
            17  => Covered_YN!,
            18  => Temperature!,
            19  => TripMiles!,
            20  => Inactive_YN!,
            21  => PayCarrier_YN!,
            22  => PayImportDate_YN!,
            23  => BillAllCustomers_YN!,
            24  => BillCustomerImportData_YN!,
            25  => PayTruckAmount!,
            26  => TrailerType!,
            27  => TrailerId!,
            28  => TrailerSize!,
            29  => TrailerSizeId!,
            30  => UrgentMessage!,
            31  => PalletExchange!,
            32  => ReceiverUnloading!,
            33  => UnloadingCharges!,
            34  => DeliveredOnTime!,
            35  => NeedsAttention!,
            36  => Void!,
            37  => Locked!,
            38  => Claim!,
            39  => TrailerComments!,
            40  => CustomerName!,
            41  => CustomerPO!,
            42  => CheckCallList!,
            43  => LoadStatusInfo!,
            44  => DataChanged!,
            45  => DateChanged!,
            46  => SplitLoad!,
            47  => Consignees!,
            48  => DriversCellPhone!,
            49  => DateMarkedDelivered!,
            50  => QuickBooksStatus!,
            51  => QuickBooksStatusReason!,
            52  => NoSundayCC!,
            53  => DateSentToQB!,
            54  => UniqueId!,
            55  => DateCreated!,
            56  => CreatedBy!,
            57  => Computer!,
            58  => Dispatcher!,
            59  => Pallets!,
            60  => TimeToCheckBy!,
            61  => NetworkLogon!,
            62  => AssistantId!,
            63  => RoadMiles!,
            64  => ExpenseTotal!,
            65  => ChargesTotal!,
            66  => CustomerBillTotal!,
            67  => Weight!,
            68  => ChargeCustomerUnloadingTotal!,
            69  => PayCarrierUnloadingTotal!,
            70  => CurrentCarrierStatus!,
            71  => VoidReason!,
            72  => DeliveredLoadChange!,
            73  => InternalDatSearch!,
            74  => DriverReload!,
            75  => MinPayTruck!,
            76  => MaxPayTruck!,
            77  => DateTurnedIn!,
            78  => CarrierUniqueId!,
            79  => rowguid,
            80  => NightCoverLoad!,
            81  => SendToCRS!,
            82  => NeedAppointment!,
            83  => OKForComcheck!,
            84  => ComcheckAmount!,
            85  => CSATask!,
            86  => CustomerRCDate!,
            87  => DriverUniqueId!,
            88  => Lane!,
            89  => CSAUserID!,
            90  => RecordStatus!,
            91  => LabelPrinted!,
            92  => GPStatus!,
            93  => Location!,
            94  => IDATRadius!,
            95  => LoadCommodityType!,
            96  => PickupCount!,
            97  => DropCount!,
            98  => PracticalMiles!,
            99  => ShortMiles!,
            100 => DispatcherUniqueID!,
            101 => CaseCount!,
            102 => ServiceType!,
            103 => HazmatIndicator!,
            104 => LoadValue!,
            105 => TemperatureCategory!,
            106 => LoadCommodityOther!,
            107 => LoadBoardUserId!,
            108 => RateType!,
            109 => IsCustomerChargingLateFee!,
            110 => ExactLoadValue!,
            111 => CustomerId!,
            112 => Driver2Id!,
            113 => Driver2Name!,
            114 => Driver2CellPhone!,
            115 => Driver2UniqueId!,
            116 => TeamCover!,
            117 => EmergencyCover!,
            118 => DateSentToAccounting!,
            119 => SentToAccountingByUserId!,
            120 => ReeferCheckInDate!,
            121 => ContainerNumber!,
            122 => TrailerNumber!,
            123 => __cdc_cap_tstamp,
            124 => __cdc_changedby_user,
            125 => __cdc_integ_key,
            126 => __cdc_integ_tstamp,
            127 => __cdc_op_val,
            128 => __test_seq,
            129 => __test_ts,
            _   => throw new AvroRuntimeException($"Bad index {fieldPos} in Get()")
        };
    }

    public void Put(int fieldPos, object fieldValue)
    {
        switch (fieldPos)
        {
            case 0:   Id_ = (int?)fieldValue; break;
            case 1:   LoadDate = (DateTime?)fieldValue; break;
            case 2:   SystemDate = (DateTime?)fieldValue; break;
            case 3:   PONumber = (int)fieldValue; break;
            case 4:   LoadSplitNumber = (int?)fieldValue; break;
            case 5:   CarrierId = (int?)fieldValue; break;
            case 6:   CarrierName = (string?)fieldValue; break;
            case 7:   DriverId = (int?)fieldValue; break;
            case 8:   DriverName = (string?)fieldValue; break;
            case 9:   DeliveryDate = (DateTime?)fieldValue; break;
            case 10:  Remarks = (string?)fieldValue; break;
            case 11:  SalesPersonId = (int?)fieldValue; break;
            case 12:  SalesPersonName = (string?)fieldValue; break;
            case 13:  SavedBySalesPersonId = (int?)fieldValue; break;
            case 14:  SavedBySalesPersonName = (string?)fieldValue; break;
            case 15:  CheckIn_YN = (string?)fieldValue; break;
            case 16:  Dispatched_YN = (bool?)fieldValue; break;
            case 17:  Covered_YN = (bool?)fieldValue; break;
            case 18:  Temperature = (string?)fieldValue; break;
            case 19:  TripMiles = (string?)fieldValue; break;
            case 20:  Inactive_YN = (bool?)fieldValue; break;
            case 21:  PayCarrier_YN = (bool?)fieldValue; break;
            case 22:  PayImportDate_YN = (bool?)fieldValue; break;
            case 23:  BillAllCustomers_YN = (bool?)fieldValue; break;
            case 24:  BillCustomerImportData_YN = (bool?)fieldValue; break;
            case 25:  PayTruckAmount = (AvroDecimal?)fieldValue; break;
            case 26:  TrailerType = (string?)fieldValue; break;
            case 27:  TrailerId = (int?)fieldValue; break;
            case 28:  TrailerSize = (string?)fieldValue; break;
            case 29:  TrailerSizeId = (int?)fieldValue; break;
            case 30:  UrgentMessage = (bool?)fieldValue; break;
            case 31:  PalletExchange = (bool?)fieldValue; break;
            case 32:  ReceiverUnloading = (bool?)fieldValue; break;
            case 33:  UnloadingCharges = (int?)fieldValue; break;
            case 34:  DeliveredOnTime = (bool?)fieldValue; break;
            case 35:  NeedsAttention = (bool?)fieldValue; break;
            case 36:  Void = (bool?)fieldValue; break;
            case 37:  Locked = (bool?)fieldValue; break;
            case 38:  Claim = (bool?)fieldValue; break;
            case 39:  TrailerComments = (string?)fieldValue; break;
            case 40:  CustomerName = (string?)fieldValue; break;
            case 41:  CustomerPO = (string?)fieldValue; break;
            case 42:  CheckCallList = (int?)fieldValue; break;
            case 43:  LoadStatusInfo = (int?)fieldValue; break;
            case 44:  DataChanged = (int?)fieldValue; break;
            case 45:  DateChanged = (DateTime?)fieldValue; break;
            case 46:  SplitLoad = (bool?)fieldValue; break;
            case 47:  Consignees = (string?)fieldValue; break;
            case 48:  DriversCellPhone = (string?)fieldValue; break;
            case 49:  DateMarkedDelivered = (DateTime?)fieldValue; break;
            case 50:  QuickBooksStatus = (int?)fieldValue; break;
            case 51:  QuickBooksStatusReason = (string?)fieldValue; break;
            case 52:  NoSundayCC = (bool?)fieldValue; break;
            case 53:  DateSentToQB = (DateTime?)fieldValue; break;
            case 54:  UniqueId = (string?)fieldValue; break;
            case 55:  DateCreated = (DateTime?)fieldValue; break;
            case 56:  CreatedBy = (string?)fieldValue; break;
            case 57:  Computer = (string?)fieldValue; break;
            case 58:  Dispatcher = (string?)fieldValue; break;
            case 59:  Pallets = (int?)fieldValue; break;
            case 60:  TimeToCheckBy = (DateTime?)fieldValue; break;
            case 61:  NetworkLogon = (string?)fieldValue; break;
            case 62:  AssistantId = (int?)fieldValue; break;
            case 63:  RoadMiles = (int?)fieldValue; break;
            case 64:  ExpenseTotal = (AvroDecimal?)fieldValue; break;
            case 65:  ChargesTotal = (AvroDecimal?)fieldValue; break;
            case 66:  CustomerBillTotal = (AvroDecimal?)fieldValue; break;
            case 67:  Weight = (int?)fieldValue; break;
            case 68:  ChargeCustomerUnloadingTotal = (AvroDecimal?)fieldValue; break;
            case 69:  PayCarrierUnloadingTotal = (AvroDecimal?)fieldValue; break;
            case 70:  CurrentCarrierStatus = (int?)fieldValue; break;
            case 71:  VoidReason = (string?)fieldValue; break;
            case 72:  DeliveredLoadChange = (bool?)fieldValue; break;
            case 73:  InternalDatSearch = (int?)fieldValue; break;
            case 74:  DriverReload = (bool?)fieldValue; break;
            case 75:  MinPayTruck = (AvroDecimal?)fieldValue; break;
            case 76:  MaxPayTruck = (AvroDecimal?)fieldValue; break;
            case 77:  DateTurnedIn = (DateTime?)fieldValue; break;
            case 78:  CarrierUniqueId = (string?)fieldValue; break;
            case 79:  rowguid = (string)fieldValue; break;
            case 80:  NightCoverLoad = (int?)fieldValue; break;
            case 81:  SendToCRS = (bool?)fieldValue; break;
            case 82:  NeedAppointment = (int?)fieldValue; break;
            case 83:  OKForComcheck = (int?)fieldValue; break;
            case 84:  ComcheckAmount = (string?)fieldValue; break;
            case 85:  CSATask = (string?)fieldValue; break;
            case 86:  CustomerRCDate = (DateTime?)fieldValue; break;
            case 87:  DriverUniqueId = (string?)fieldValue; break;
            case 88:  Lane = (string?)fieldValue; break;
            case 89:  CSAUserID = (int?)fieldValue; break;
            case 90:  RecordStatus = (int?)fieldValue; break;
            case 91:  LabelPrinted = (int?)fieldValue; break;
            case 92:  GPStatus = (int?)fieldValue; break;
            case 93:  Location = (int?)fieldValue; break;
            case 94:  IDATRadius = (int?)fieldValue; break;
            case 95:  LoadCommodityType = (int?)fieldValue; break;
            case 96:  PickupCount = (int?)fieldValue; break;
            case 97:  DropCount = (int?)fieldValue; break;
            case 98:  PracticalMiles = (int?)fieldValue; break;
            case 99:  ShortMiles = (int?)fieldValue; break;
            case 100: DispatcherUniqueID = (string?)fieldValue; break;
            case 101: CaseCount = (int?)fieldValue; break;
            case 102: ServiceType = (int?)fieldValue; break;
            case 103: HazmatIndicator = (int?)fieldValue; break;
            case 104: LoadValue = (int?)fieldValue; break;
            case 105: TemperatureCategory = (int?)fieldValue; break;
            case 106: LoadCommodityOther = (string?)fieldValue; break;
            case 107: LoadBoardUserId = (int?)fieldValue; break;
            case 108: RateType = (int?)fieldValue; break;
            case 109: IsCustomerChargingLateFee = (bool?)fieldValue; break;
            case 110: ExactLoadValue = (AvroDecimal?)fieldValue; break;
            case 111: CustomerId = (int?)fieldValue; break;
            case 112: Driver2Id = (int?)fieldValue; break;
            case 113: Driver2Name = (string?)fieldValue; break;
            case 114: Driver2CellPhone = (string?)fieldValue; break;
            case 115: Driver2UniqueId = (string?)fieldValue; break;
            case 116: TeamCover = (bool?)fieldValue; break;
            case 117: EmergencyCover = (bool?)fieldValue; break;
            case 118: DateSentToAccounting = (DateTime?)fieldValue; break;
            case 119: SentToAccountingByUserId = (int?)fieldValue; break;
            case 120: ReeferCheckInDate = (DateTime?)fieldValue; break;
            case 121: ContainerNumber = (string?)fieldValue; break;
            case 122: TrailerNumber = (string?)fieldValue; break;
            case 123: __cdc_cap_tstamp = (string)fieldValue; break;
            case 124: __cdc_changedby_user = (string)fieldValue; break;
            case 125: __cdc_integ_key = (string)fieldValue; break;
            case 126: __cdc_integ_tstamp = (string)fieldValue; break;
            case 127: __cdc_op_val = (int)fieldValue; break;
            case 128: __test_seq = (int)fieldValue; break;
            case 129: __test_ts = (string)fieldValue; break;
            default: throw new AvroRuntimeException($"Bad index {fieldPos} in Put()");
        }
    }
}
