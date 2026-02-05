using System.Text.Json.Serialization;

namespace ConfluentThroughputTestHarness.Models;

public class FreightDboTblLoadsSmall
{
    [JsonPropertyName("Id_")]
    public int? Id_ { get; set; }

    [JsonPropertyName("LoadDate")]
    public long? LoadDate { get; set; }

    [JsonPropertyName("PONumber")]
    public int PONumber { get; set; }

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

    [JsonPropertyName("Dispatched_YN")]
    public bool? Dispatched_YN { get; set; }

    [JsonPropertyName("Covered_YN")]
    public bool? Covered_YN { get; set; }

    [JsonPropertyName("PayTruckAmount")]
    public decimal? PayTruckAmount { get; set; }

    [JsonPropertyName("TrailerType")]
    public string? TrailerType { get; set; }

    [JsonPropertyName("CustomerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("CustomerPO")]
    public string? CustomerPO { get; set; }

    [JsonPropertyName("DateChanged")]
    public long? DateChanged { get; set; }

    [JsonPropertyName("UniqueId")]
    public string? UniqueId { get; set; }

    [JsonPropertyName("DateCreated")]
    public long? DateCreated { get; set; }

    [JsonPropertyName("ExpenseTotal")]
    public decimal? ExpenseTotal { get; set; }

    [JsonPropertyName("ChargesTotal")]
    public decimal? ChargesTotal { get; set; }

    [JsonPropertyName("Weight")]
    public int? Weight { get; set; }

    [JsonPropertyName("RoadMiles")]
    public int? RoadMiles { get; set; }

    [JsonPropertyName("rowguid")]
    public string rowguid { get; set; } = string.Empty;

    [JsonPropertyName("__cdc_integ_key")]
    public string __cdc_integ_key { get; set; } = string.Empty;

    [JsonPropertyName("__cdc_op_val")]
    public int __cdc_op_val { get; set; }
}
