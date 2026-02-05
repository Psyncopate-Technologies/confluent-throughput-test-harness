// ────────────────────────────────────────────────────────────────────
// AvroLargeDataFactory.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Builds an Avro GenericRecord populated with sample data
//           for the large (104-field) freight CDC tblloads schema.
//           Used to produce test messages for the Avro large producer
//           benchmark.
// ────────────────────────────────────────────────────────────────────

using Avro;
using Avro.Generic;

namespace ConfluentThroughputTestHarness.DataFactories;

public class AvroLargeDataFactory : ITestDataFactory<GenericRecord>
{
    private readonly RecordSchema _schema;

    public AvroLargeDataFactory(RecordSchema schema)
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
        record.Add("SystemDate", now);
        record.Add("PONumber", 50001);
        record.Add("LoadSplitNumber", 1);
        record.Add("CarrierId", 2001);
        record.Add("CarrierName", "FastFreight Logistics LLC");
        record.Add("DriverId", 3001);
        record.Add("DriverName", "John Smith");
        record.Add("DeliveryDate", now);
        record.Add("Remarks", "Standard delivery - no special handling required. Customer has dock access available 7AM-5PM weekdays.");
        record.Add("SalesPersonId", 401);
        record.Add("SalesPersonName", "Jane Williams");
        record.Add("SavedBySalesPersonId", 402);
        record.Add("SavedBySalesPersonName", "Bob Johnson");
        record.Add("CheckIn_YN", "Yes");
        record.Add("Dispatched_YN", true);
        record.Add("Covered_YN", true);
        record.Add("Temperature", "34F");
        record.Add("TripMiles", "487");
        record.Add("Inactive_YN", false);
        record.Add("PayCarrier_YN", true);
        record.Add("PayImportDate_YN", false);
        record.Add("BillAllCustomers_YN", true);
        record.Add("BillCustomerImportData_YN", false);
        record.Add("PayTruckAmount", new AvroDecimal(2500.0000m));
        record.Add("TrailerType", "Refrigerated");
        record.Add("TrailerId", 5001);
        record.Add("TrailerSize", "53ft");
        record.Add("TrailerSizeId", 2);
        record.Add("UrgentMessage", false);
        record.Add("PalletExchange", true);
        record.Add("ReceiverUnloading", true);
        record.Add("UnloadingCharges", 150);
        record.Add("DeliveredOnTime", true);
        record.Add("NeedsAttention", false);
        record.Add("Void", false);
        record.Add("Locked", false);
        record.Add("Claim", false);
        record.Add("TrailerComments", "Trailer in good condition, reefer unit running at set point.");
        record.Add("CustomerName", "Acme Food Distribution Inc.");
        record.Add("CustomerPO", "PO-2024-78543");
        record.Add("CheckCallList", 3);
        record.Add("LoadStatusInfo", 5);
        record.Add("DataChanged", 1);
        record.Add("DateChanged", now);
        record.Add("SplitLoad", false);
        record.Add("Consignees", "Warehouse A - Building 7, Dock 12");
        record.Add("DriversCellPhone", "555-867-5309");
        record.Add("DateMarkedDelivered", now);
        record.Add("QuickBooksStatus", 2);
        record.Add("QuickBooksStatusReason", "Synced");
        record.Add("NoSundayCC", false);
        record.Add("DateSentToQB", now);
        record.Add("UniqueId", guid);
        record.Add("DateCreated", now);
        record.Add("CreatedBy", "system_import");
        record.Add("Computer", "DISPATCH-PC-04");
        record.Add("Dispatcher", "Mary Thompson");
        record.Add("Pallets", 22);
        record.Add("TimeToCheckBy", now);
        record.Add("NetworkLogon", "mthompson");
        record.Add("AssistantId", 601);
        record.Add("RoadMiles", 487);
        record.Add("ExpenseTotal", new AvroDecimal(3250.0000m));
        record.Add("ChargesTotal", new AvroDecimal(4100.0000m));
        record.Add("CustomerBillTotal", new AvroDecimal(4500.0000m));
        record.Add("Weight", 42000);
        record.Add("ChargeCustomerUnloadingTotal", new AvroDecimal(150.0000m));
        record.Add("PayCarrierUnloadingTotal", new AvroDecimal(100.0000m));
        record.Add("CurrentCarrierStatus", 4);
        record.Add("VoidReason", null);
        record.Add("DeliveredLoadChange", false);
        record.Add("InternalDatSearch", 0);
        record.Add("DriverReload", false);
        record.Add("MinPayTruck", new AvroDecimal(2000.0000m));
        record.Add("MaxPayTruck", new AvroDecimal(3000.0000m));
        record.Add("DateTurnedIn", now);
        record.Add("CarrierUniqueId", Guid.NewGuid().ToString());
        record.Add("rowguid", Guid.NewGuid().ToString("D").Substring(0, 36));
        record.Add("NightCoverLoad", 0);
        record.Add("SendToCRS", false);
        record.Add("NeedAppointment", 1);
        record.Add("OKForComcheck", 1);
        record.Add("ComcheckAmount", "500.00");
        record.Add("CSATask", "Standard safety verification complete");
        record.Add("CustomerRCDate", now);
        record.Add("DriverUniqueId", Guid.NewGuid().ToString());
        record.Add("Lane", "CHI-DAL");
        record.Add("CSAUserID", 701);
        record.Add("RecordStatus", 1);
        record.Add("LabelPrinted", 1);
        record.Add("GPStatus", 0);
        record.Add("Location", 3);
        record.Add("IDATRadius", 100);
        record.Add("LoadCommodityType", 2);
        record.Add("PickupCount", 1);
        record.Add("DropCount", 1);
        record.Add("PracticalMiles", 492);
        record.Add("ShortMiles", 480);
        record.Add("DispatcherUniqueID", Guid.NewGuid().ToString());
        record.Add("CaseCount", 440);
        record.Add("ServiceType", 1);
        record.Add("HazmatIndicator", 0);
        record.Add("LoadValue", 3);
        record.Add("TemperatureCategory", 2);
        record.Add("LoadCommodityOther", null);
        record.Add("LoadBoardUserId", 801);
        record.Add("RateType", 1);
        record.Add("IsCustomerChargingLateFee", false);
        record.Add("ExactLoadValue", new AvroDecimal(4500.00m));
        record.Add("CustomerId", 9001);
        record.Add("Driver2Id", null);
        record.Add("Driver2Name", null);
        record.Add("Driver2CellPhone", null);
        record.Add("Driver2UniqueId", null);
        record.Add("TeamCover", false);
        record.Add("EmergencyCover", false);
        record.Add("DateSentToAccounting", now);
        record.Add("SentToAccountingByUserId", 402);
        record.Add("ReeferCheckInDate", now);
        record.Add("ContainerNumber", "CNTR-2024-98765");
        record.Add("TrailerNumber", "TRL-53-4421");
        record.Add("__cdc_cap_tstamp", "2024-01-15T10:30:00.000Z");
        record.Add("__cdc_changedby_user", "cdc_service");
        record.Add("__cdc_integ_key", "100001");
        record.Add("__cdc_integ_tstamp", "2024-01-15T10:30:00.000Z");
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
