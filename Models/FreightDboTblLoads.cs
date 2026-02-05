using System.Text.Json.Serialization;

namespace ConfluentThroughputTestHarness.Models;

/// <summary>
/// JSON POCO matching the schema-cdc_freight_dbo_tblloads-value-v4.avsc schema (104 fields).
/// </summary>
public class FreightDboTblLoads
{
    [JsonPropertyName("Id_")]
    public int? Id_ { get; set; }

    [JsonPropertyName("LoadDate")]
    public long? LoadDate { get; set; }

    [JsonPropertyName("SystemDate")]
    public long? SystemDate { get; set; }

    [JsonPropertyName("PONumber")]
    public int PONumber { get; set; }

    [JsonPropertyName("LoadSplitNumber")]
    public int? LoadSplitNumber { get; set; }

    [JsonPropertyName("CarrierId")]
    public int? CarrierId { get; set; }

    [JsonPropertyName("CarrierName")]
    public string? CarrierName { get; set; }

    [JsonPropertyName("DriverId")]
    public int? DriverId { get; set; }

    [JsonPropertyName("DriverName")]
    public string? DriverName { get; set; }

    [JsonPropertyName("DeliveryDate")]
    public long? DeliveryDate { get; set; }

    [JsonPropertyName("Remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("SalesPersonId")]
    public int? SalesPersonId { get; set; }

    [JsonPropertyName("SalesPersonName")]
    public string? SalesPersonName { get; set; }

    [JsonPropertyName("SavedBySalesPersonId")]
    public int? SavedBySalesPersonId { get; set; }

    [JsonPropertyName("SavedBySalesPersonName")]
    public string? SavedBySalesPersonName { get; set; }

    [JsonPropertyName("CheckIn_YN")]
    public string? CheckIn_YN { get; set; }

    [JsonPropertyName("Dispatched_YN")]
    public bool? Dispatched_YN { get; set; }

    [JsonPropertyName("Covered_YN")]
    public bool? Covered_YN { get; set; }

    [JsonPropertyName("Temperature")]
    public string? Temperature { get; set; }

    [JsonPropertyName("TripMiles")]
    public string? TripMiles { get; set; }

    [JsonPropertyName("Inactive_YN")]
    public bool? Inactive_YN { get; set; }

    [JsonPropertyName("PayCarrier_YN")]
    public bool? PayCarrier_YN { get; set; }

    [JsonPropertyName("PayImportDate_YN")]
    public bool? PayImportDate_YN { get; set; }

    [JsonPropertyName("BillAllCustomers_YN")]
    public bool? BillAllCustomers_YN { get; set; }

    [JsonPropertyName("BillCustomerImportData_YN")]
    public bool? BillCustomerImportData_YN { get; set; }

    [JsonPropertyName("PayTruckAmount")]
    public decimal? PayTruckAmount { get; set; }

    [JsonPropertyName("TrailerType")]
    public string? TrailerType { get; set; }

    [JsonPropertyName("TrailerId")]
    public int? TrailerId { get; set; }

    [JsonPropertyName("TrailerSize")]
    public string? TrailerSize { get; set; }

    [JsonPropertyName("TrailerSizeId")]
    public int? TrailerSizeId { get; set; }

    [JsonPropertyName("UrgentMessage")]
    public bool? UrgentMessage { get; set; }

    [JsonPropertyName("PalletExchange")]
    public bool? PalletExchange { get; set; }

    [JsonPropertyName("ReceiverUnloading")]
    public bool? ReceiverUnloading { get; set; }

    [JsonPropertyName("UnloadingCharges")]
    public int? UnloadingCharges { get; set; }

    [JsonPropertyName("DeliveredOnTime")]
    public bool? DeliveredOnTime { get; set; }

    [JsonPropertyName("NeedsAttention")]
    public bool? NeedsAttention { get; set; }

    [JsonPropertyName("Void")]
    public bool? Void { get; set; }

    [JsonPropertyName("Locked")]
    public bool? Locked { get; set; }

    [JsonPropertyName("Claim")]
    public bool? Claim { get; set; }

    [JsonPropertyName("TrailerComments")]
    public string? TrailerComments { get; set; }

    [JsonPropertyName("CustomerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("CustomerPO")]
    public string? CustomerPO { get; set; }

    [JsonPropertyName("CheckCallList")]
    public int? CheckCallList { get; set; }

    [JsonPropertyName("LoadStatusInfo")]
    public int? LoadStatusInfo { get; set; }

    [JsonPropertyName("DataChanged")]
    public int? DataChanged { get; set; }

    [JsonPropertyName("DateChanged")]
    public long? DateChanged { get; set; }

    [JsonPropertyName("SplitLoad")]
    public bool? SplitLoad { get; set; }

    [JsonPropertyName("Consignees")]
    public string? Consignees { get; set; }

    [JsonPropertyName("DriversCellPhone")]
    public string? DriversCellPhone { get; set; }

    [JsonPropertyName("DateMarkedDelivered")]
    public long? DateMarkedDelivered { get; set; }

    [JsonPropertyName("QuickBooksStatus")]
    public int? QuickBooksStatus { get; set; }

    [JsonPropertyName("QuickBooksStatusReason")]
    public string? QuickBooksStatusReason { get; set; }

    [JsonPropertyName("NoSundayCC")]
    public bool? NoSundayCC { get; set; }

    [JsonPropertyName("DateSentToQB")]
    public long? DateSentToQB { get; set; }

    [JsonPropertyName("UniqueId")]
    public string? UniqueId { get; set; }

    [JsonPropertyName("DateCreated")]
    public long? DateCreated { get; set; }

    [JsonPropertyName("CreatedBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("Computer")]
    public string? Computer { get; set; }

    [JsonPropertyName("Dispatcher")]
    public string? Dispatcher { get; set; }

    [JsonPropertyName("Pallets")]
    public int? Pallets { get; set; }

    [JsonPropertyName("TimeToCheckBy")]
    public long? TimeToCheckBy { get; set; }

    [JsonPropertyName("NetworkLogon")]
    public string? NetworkLogon { get; set; }

    [JsonPropertyName("AssistantId")]
    public int? AssistantId { get; set; }

    [JsonPropertyName("RoadMiles")]
    public int? RoadMiles { get; set; }

    [JsonPropertyName("ExpenseTotal")]
    public decimal? ExpenseTotal { get; set; }

    [JsonPropertyName("ChargesTotal")]
    public decimal? ChargesTotal { get; set; }

    [JsonPropertyName("CustomerBillTotal")]
    public decimal? CustomerBillTotal { get; set; }

    [JsonPropertyName("Weight")]
    public int? Weight { get; set; }

    [JsonPropertyName("ChargeCustomerUnloadingTotal")]
    public decimal? ChargeCustomerUnloadingTotal { get; set; }

    [JsonPropertyName("PayCarrierUnloadingTotal")]
    public decimal? PayCarrierUnloadingTotal { get; set; }

    [JsonPropertyName("CurrentCarrierStatus")]
    public int? CurrentCarrierStatus { get; set; }

    [JsonPropertyName("VoidReason")]
    public string? VoidReason { get; set; }

    [JsonPropertyName("DeliveredLoadChange")]
    public bool? DeliveredLoadChange { get; set; }

    [JsonPropertyName("InternalDatSearch")]
    public int? InternalDatSearch { get; set; }

    [JsonPropertyName("DriverReload")]
    public bool? DriverReload { get; set; }

    [JsonPropertyName("MinPayTruck")]
    public decimal? MinPayTruck { get; set; }

    [JsonPropertyName("MaxPayTruck")]
    public decimal? MaxPayTruck { get; set; }

    [JsonPropertyName("DateTurnedIn")]
    public long? DateTurnedIn { get; set; }

    [JsonPropertyName("CarrierUniqueId")]
    public string? CarrierUniqueId { get; set; }

    [JsonPropertyName("rowguid")]
    public string rowguid { get; set; } = string.Empty;

    [JsonPropertyName("NightCoverLoad")]
    public int? NightCoverLoad { get; set; }

    [JsonPropertyName("SendToCRS")]
    public bool? SendToCRS { get; set; }

    [JsonPropertyName("NeedAppointment")]
    public int? NeedAppointment { get; set; }

    [JsonPropertyName("OKForComcheck")]
    public int? OKForComcheck { get; set; }

    [JsonPropertyName("ComcheckAmount")]
    public string? ComcheckAmount { get; set; }

    [JsonPropertyName("CSATask")]
    public string? CSATask { get; set; }

    [JsonPropertyName("CustomerRCDate")]
    public long? CustomerRCDate { get; set; }

    [JsonPropertyName("DriverUniqueId")]
    public string? DriverUniqueId { get; set; }

    [JsonPropertyName("Lane")]
    public string? Lane { get; set; }

    [JsonPropertyName("CSAUserID")]
    public int? CSAUserID { get; set; }

    [JsonPropertyName("RecordStatus")]
    public int? RecordStatus { get; set; }

    [JsonPropertyName("LabelPrinted")]
    public int? LabelPrinted { get; set; }

    [JsonPropertyName("GPStatus")]
    public int? GPStatus { get; set; }

    [JsonPropertyName("Location")]
    public int? Location { get; set; }

    [JsonPropertyName("IDATRadius")]
    public int? IDATRadius { get; set; }

    [JsonPropertyName("LoadCommodityType")]
    public int? LoadCommodityType { get; set; }

    [JsonPropertyName("PickupCount")]
    public int? PickupCount { get; set; }

    [JsonPropertyName("DropCount")]
    public int? DropCount { get; set; }

    [JsonPropertyName("PracticalMiles")]
    public int? PracticalMiles { get; set; }

    [JsonPropertyName("ShortMiles")]
    public int? ShortMiles { get; set; }

    [JsonPropertyName("DispatcherUniqueID")]
    public string? DispatcherUniqueID { get; set; }

    [JsonPropertyName("CaseCount")]
    public int? CaseCount { get; set; }

    [JsonPropertyName("ServiceType")]
    public int? ServiceType { get; set; }

    [JsonPropertyName("HazmatIndicator")]
    public int? HazmatIndicator { get; set; }

    [JsonPropertyName("LoadValue")]
    public int? LoadValue { get; set; }

    [JsonPropertyName("TemperatureCategory")]
    public int? TemperatureCategory { get; set; }

    [JsonPropertyName("LoadCommodityOther")]
    public string? LoadCommodityOther { get; set; }

    [JsonPropertyName("LoadBoardUserId")]
    public int? LoadBoardUserId { get; set; }

    [JsonPropertyName("RateType")]
    public int? RateType { get; set; }

    [JsonPropertyName("IsCustomerChargingLateFee")]
    public bool? IsCustomerChargingLateFee { get; set; }

    [JsonPropertyName("ExactLoadValue")]
    public decimal? ExactLoadValue { get; set; }

    [JsonPropertyName("CustomerId")]
    public int? CustomerId { get; set; }

    [JsonPropertyName("Driver2Id")]
    public int? Driver2Id { get; set; }

    [JsonPropertyName("Driver2Name")]
    public string? Driver2Name { get; set; }

    [JsonPropertyName("Driver2CellPhone")]
    public string? Driver2CellPhone { get; set; }

    [JsonPropertyName("Driver2UniqueId")]
    public string? Driver2UniqueId { get; set; }

    [JsonPropertyName("TeamCover")]
    public bool? TeamCover { get; set; }

    [JsonPropertyName("EmergencyCover")]
    public bool? EmergencyCover { get; set; }

    [JsonPropertyName("DateSentToAccounting")]
    public long? DateSentToAccounting { get; set; }

    [JsonPropertyName("SentToAccountingByUserId")]
    public int? SentToAccountingByUserId { get; set; }

    [JsonPropertyName("ReeferCheckInDate")]
    public long? ReeferCheckInDate { get; set; }

    [JsonPropertyName("ContainerNumber")]
    public string? ContainerNumber { get; set; }

    [JsonPropertyName("TrailerNumber")]
    public string? TrailerNumber { get; set; }

    [JsonPropertyName("__cdc_cap_tstamp")]
    public string __cdc_cap_tstamp { get; set; } = string.Empty;

    [JsonPropertyName("__cdc_changedby_user")]
    public string __cdc_changedby_user { get; set; } = string.Empty;

    [JsonPropertyName("__cdc_integ_key")]
    public string __cdc_integ_key { get; set; } = string.Empty;

    [JsonPropertyName("__cdc_integ_tstamp")]
    public string __cdc_integ_tstamp { get; set; } = string.Empty;

    [JsonPropertyName("__cdc_op_val")]
    public int __cdc_op_val { get; set; }
}
