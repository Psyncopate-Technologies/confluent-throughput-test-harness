using System.Text.Json.Serialization;

namespace ConfluentThroughputTestHarness.Models;

/// <summary>
/// JSON POCO matching the TestAvroDataTypesMsg.g.avsc schema (58 fields).
/// </summary>
public class TestAvroDataTypesMsg
{
    [JsonPropertyName("Guid1")]
    public string Guid1 { get; set; } = string.Empty;

    [JsonPropertyName("Guid1N")]
    public string? Guid1N { get; set; }

    [JsonPropertyName("Guid1NV")]
    public string Guid1NV { get; set; } = string.Empty;

    [JsonPropertyName("Bool1")]
    public bool Bool1 { get; set; }

    [JsonPropertyName("Bool1N")]
    public bool? Bool1N { get; set; }

    [JsonPropertyName("Bool1NV")]
    public bool? Bool1NV { get; set; }

    [JsonPropertyName("String1")]
    public string String1 { get; set; } = string.Empty;

    [JsonPropertyName("String1N")]
    public string? String1N { get; set; }

    [JsonPropertyName("String1NV")]
    public string? String1NV { get; set; }

    [JsonPropertyName("Int1")]
    public int Int1 { get; set; }

    [JsonPropertyName("Int1N")]
    public int? Int1N { get; set; }

    [JsonPropertyName("Int1NV")]
    public int? Int1NV { get; set; }

    [JsonPropertyName("Long1")]
    public long Long1 { get; set; }

    [JsonPropertyName("Long1N")]
    public long? Long1N { get; set; }

    [JsonPropertyName("Long1NV")]
    public long? Long1NV { get; set; }

    [JsonPropertyName("Float1")]
    public float Float1 { get; set; }

    [JsonPropertyName("Float1N")]
    public float? Float1N { get; set; }

    [JsonPropertyName("Float1NV")]
    public float? Float1NV { get; set; }

    [JsonPropertyName("Double1")]
    public double Double1 { get; set; }

    [JsonPropertyName("Double1N")]
    public double? Double1N { get; set; }

    [JsonPropertyName("Double1NV")]
    public double? Double1NV { get; set; }

    [JsonPropertyName("Decimal1")]
    public decimal Decimal1 { get; set; }

    [JsonPropertyName("Decimal1N")]
    public decimal? Decimal1N { get; set; }

    [JsonPropertyName("Decimal1NV")]
    public decimal? Decimal1NV { get; set; }

    [JsonPropertyName("Decimal2")]
    public decimal Decimal2 { get; set; }

    [JsonPropertyName("Decimal2N")]
    public decimal? Decimal2N { get; set; }

    [JsonPropertyName("Decimal2NV")]
    public decimal? Decimal2NV { get; set; }

    [JsonPropertyName("Decimal4")]
    public decimal Decimal4 { get; set; }

    [JsonPropertyName("Decimal4N")]
    public decimal? Decimal4N { get; set; }

    [JsonPropertyName("Decimal4NV")]
    public decimal? Decimal4NV { get; set; }

    [JsonPropertyName("DTUnspecified")]
    public long DTUnspecified { get; set; }

    [JsonPropertyName("DTUnspecifiedN")]
    public long? DTUnspecifiedN { get; set; }

    [JsonPropertyName("DTUnspecifiedNV")]
    public long? DTUnspecifiedNV { get; set; }

    [JsonPropertyName("DTUTC")]
    public long DTUTC { get; set; }

    [JsonPropertyName("DTUTCN")]
    public long? DTUTCN { get; set; }

    [JsonPropertyName("DTUTCNV")]
    public long? DTUTCNV { get; set; }

    [JsonPropertyName("DTUTCMillis")]
    public long DTUTCMillis { get; set; }

    [JsonPropertyName("DTUTCMicros")]
    public long DTUTCMicros { get; set; }

    [JsonPropertyName("DTLocal")]
    public long DTLocal { get; set; }

    [JsonPropertyName("DTLocalN")]
    public long? DTLocalN { get; set; }

    [JsonPropertyName("DTLocalNV")]
    public long? DTLocalNV { get; set; }

    [JsonPropertyName("DTLocalMillis")]
    public long DTLocalMillis { get; set; }

    [JsonPropertyName("DTLocalMicros")]
    public long DTLocalMicros { get; set; }

    [JsonPropertyName("DTOUTC1")]
    public long DTOUTC1 { get; set; }

    [JsonPropertyName("DTOUTC1N")]
    public long? DTOUTC1N { get; set; }

    [JsonPropertyName("DTOUTC1NV")]
    public long? DTOUTC1NV { get; set; }

    [JsonPropertyName("DTOLocal1")]
    public long DTOLocal1 { get; set; }

    [JsonPropertyName("DTOLocal1N")]
    public long? DTOLocal1N { get; set; }

    [JsonPropertyName("DTOLocal1NV")]
    public long? DTOLocal1NV { get; set; }

    [JsonPropertyName("OrderStatus1")]
    public string OrderStatus1 { get; set; } = string.Empty;

    [JsonPropertyName("OrderStatus1N")]
    public string? OrderStatus1N { get; set; }

    [JsonPropertyName("OrderStatus1NV")]
    public string? OrderStatus1NV { get; set; }
}
