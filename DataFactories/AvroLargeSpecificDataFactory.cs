// ────────────────────────────────────────────────────────────────────
// AvroLargeSpecificDataFactory.cs
// Created:  2026-02-09
// Author:   Ayu Admassu
// Purpose:  Builds a FreightLargeSpecific (ISpecificRecord) populated
//           with sample data for the large (130-field) freight CDC
//           tblloads schema. Used to produce test messages for the
//           Avro large SpecificRecord producer benchmark.
// ────────────────────────────────────────────────────────────────────

using Avro;
using ConfluentThroughputTestHarness.Models.AvroSpecific;

namespace ConfluentThroughputTestHarness.DataFactories;

public class AvroLargeSpecificDataFactory : ITestDataFactory<FreightLargeSpecific>
{
    public FreightLargeSpecific CreateRecord()
    {
        var now = DateTime.UtcNow;
        var guid = Guid.NewGuid().ToString();

        return new FreightLargeSpecific
        {
            Id_ = 100001,
            LoadDate = now,
            SystemDate = now,
            PONumber = 50001,
            LoadSplitNumber = 1,
            CarrierId = 2001,
            CarrierName = "FastFreight Logistics LLC",
            DriverId = 3001,
            DriverName = "John Smith",
            DeliveryDate = now,
            Remarks = "Standard delivery - no special handling required. Customer has dock access available 7AM-5PM weekdays.",
            SalesPersonId = 401,
            SalesPersonName = "Jane Williams",
            SavedBySalesPersonId = 402,
            SavedBySalesPersonName = "Bob Johnson",
            CheckIn_YN = "Yes",
            Dispatched_YN = true,
            Covered_YN = true,
            Temperature = "34F",
            TripMiles = "487",
            Inactive_YN = false,
            PayCarrier_YN = true,
            PayImportDate_YN = false,
            BillAllCustomers_YN = true,
            BillCustomerImportData_YN = false,
            PayTruckAmount = new AvroDecimal(2500.0000m),
            TrailerType = "Refrigerated",
            TrailerId = 5001,
            TrailerSize = "53ft",
            TrailerSizeId = 2,
            UrgentMessage = false,
            PalletExchange = true,
            ReceiverUnloading = true,
            UnloadingCharges = 150,
            DeliveredOnTime = true,
            NeedsAttention = false,
            Void = false,
            Locked = false,
            Claim = false,
            TrailerComments = "Trailer in good condition, reefer unit running at set point.",
            CustomerName = "Acme Food Distribution Inc.",
            CustomerPO = "PO-2024-78543",
            CheckCallList = 3,
            LoadStatusInfo = 5,
            DataChanged = 1,
            DateChanged = now,
            SplitLoad = false,
            Consignees = "Warehouse A - Building 7, Dock 12",
            DriversCellPhone = "555-867-5309",
            DateMarkedDelivered = now,
            QuickBooksStatus = 2,
            QuickBooksStatusReason = "Synced",
            NoSundayCC = false,
            DateSentToQB = now,
            UniqueId = guid,
            DateCreated = now,
            CreatedBy = "system_import",
            Computer = "DISPATCH-PC-04",
            Dispatcher = "Mary Thompson",
            Pallets = 22,
            TimeToCheckBy = now,
            NetworkLogon = "mthompson",
            AssistantId = 601,
            RoadMiles = 487,
            ExpenseTotal = new AvroDecimal(3250.0000m),
            ChargesTotal = new AvroDecimal(4100.0000m),
            CustomerBillTotal = new AvroDecimal(4500.0000m),
            Weight = 42000,
            ChargeCustomerUnloadingTotal = new AvroDecimal(150.0000m),
            PayCarrierUnloadingTotal = new AvroDecimal(100.0000m),
            CurrentCarrierStatus = 4,
            VoidReason = null,
            DeliveredLoadChange = false,
            InternalDatSearch = 0,
            DriverReload = false,
            MinPayTruck = new AvroDecimal(2000.0000m),
            MaxPayTruck = new AvroDecimal(3000.0000m),
            DateTurnedIn = now,
            CarrierUniqueId = Guid.NewGuid().ToString(),
            rowguid = Guid.NewGuid().ToString("D")[..36],
            NightCoverLoad = 0,
            SendToCRS = false,
            NeedAppointment = 1,
            OKForComcheck = 1,
            ComcheckAmount = "500.00",
            CSATask = "Standard safety verification complete",
            CustomerRCDate = now,
            DriverUniqueId = Guid.NewGuid().ToString(),
            Lane = "CHI-DAL",
            CSAUserID = 701,
            RecordStatus = 1,
            LabelPrinted = 1,
            GPStatus = 0,
            Location = 3,
            IDATRadius = 100,
            LoadCommodityType = 2,
            PickupCount = 1,
            DropCount = 1,
            PracticalMiles = 492,
            ShortMiles = 480,
            DispatcherUniqueID = Guid.NewGuid().ToString(),
            CaseCount = 440,
            ServiceType = 1,
            HazmatIndicator = 0,
            LoadValue = 3,
            TemperatureCategory = 2,
            LoadCommodityOther = null,
            LoadBoardUserId = 801,
            RateType = 1,
            IsCustomerChargingLateFee = false,
            ExactLoadValue = new AvroDecimal(4500.00m),
            CustomerId = 9001,
            Driver2Id = null,
            Driver2Name = null,
            Driver2CellPhone = null,
            Driver2UniqueId = null,
            TeamCover = false,
            EmergencyCover = false,
            DateSentToAccounting = now,
            SentToAccountingByUserId = 402,
            ReeferCheckInDate = now,
            ContainerNumber = "CNTR-2024-98765",
            TrailerNumber = "TRL-53-4421",
            __cdc_cap_tstamp = "2024-01-15T10:30:00.000Z",
            __cdc_changedby_user = "cdc_service",
            __cdc_integ_key = "100001",
            __cdc_integ_tstamp = "2024-01-15T10:30:00.000Z",
            __cdc_op_val = 1,
            __test_seq = 0,
            __test_ts = "",
        };
    }

    public void SetMessageHeader(FreightLargeSpecific record, int sequenceNumber, string timestamp)
    {
        record.__test_seq = sequenceNumber;
        record.__test_ts = timestamp;
    }
}
