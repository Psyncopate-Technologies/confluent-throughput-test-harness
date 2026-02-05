// ────────────────────────────────────────────────────────────────────
// TestDefinition.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Defines the test matrix enums (format, payload size, type)
//           and the TestDefinition class that maps each test ID to its
//           topic, serialization format, runs, and message count.
// ────────────────────────────────────────────────────────────────────

namespace ConfluentThroughputTestHarness.Tests;

public enum SerializationFormat { Avro, Json }
public enum PayloadSize { Small, Large }
public enum TestType { Producer, Consumer }

public class TestDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TestType Type { get; init; }
    public SerializationFormat Format { get; init; }
    public PayloadSize Size { get; init; }
    public string Topic { get; init; } = string.Empty;
    public int MessageCount { get; init; }
    public int Runs { get; init; }

    public static List<TestDefinition> GetAll(Config.TestSettings settings) =>
    [
        // Producer tests
        new()
        {
            Id = "T1.1", Name = "Producer Avro Small",
            Type = TestType.Producer, Format = SerializationFormat.Avro,
            Size = PayloadSize.Small, Topic = settings.AvroSmallTopic,
            MessageCount = settings.MessageCount, Runs = settings.ProducerRuns
        },
        new()
        {
            Id = "T1.2", Name = "Producer Avro Large",
            Type = TestType.Producer, Format = SerializationFormat.Avro,
            Size = PayloadSize.Large, Topic = settings.AvroLargeTopic,
            MessageCount = settings.MessageCount, Runs = settings.ProducerRuns
        },
        new()
        {
            Id = "T1.3", Name = "Producer JSON Small",
            Type = TestType.Producer, Format = SerializationFormat.Json,
            Size = PayloadSize.Small, Topic = settings.JsonSmallTopic,
            MessageCount = settings.MessageCount, Runs = settings.ProducerRuns
        },
        new()
        {
            Id = "T1.4", Name = "Producer JSON Large",
            Type = TestType.Producer, Format = SerializationFormat.Json,
            Size = PayloadSize.Large, Topic = settings.JsonLargeTopic,
            MessageCount = settings.MessageCount, Runs = settings.ProducerRuns
        },

        // Consumer tests
        new()
        {
            Id = "T2.1", Name = "Consumer Avro Small",
            Type = TestType.Consumer, Format = SerializationFormat.Avro,
            Size = PayloadSize.Small, Topic = settings.AvroSmallTopic,
            MessageCount = settings.MessageCount, Runs = settings.ConsumerRuns
        },
        new()
        {
            Id = "T2.2", Name = "Consumer Avro Large",
            Type = TestType.Consumer, Format = SerializationFormat.Avro,
            Size = PayloadSize.Large, Topic = settings.AvroLargeTopic,
            MessageCount = settings.MessageCount, Runs = settings.ConsumerRuns
        },
        new()
        {
            Id = "T2.3", Name = "Consumer JSON Small",
            Type = TestType.Consumer, Format = SerializationFormat.Json,
            Size = PayloadSize.Small, Topic = settings.JsonSmallTopic,
            MessageCount = settings.MessageCount, Runs = settings.ConsumerRuns
        },
        new()
        {
            Id = "T2.4", Name = "Consumer JSON Large",
            Type = TestType.Consumer, Format = SerializationFormat.Json,
            Size = PayloadSize.Large, Topic = settings.JsonLargeTopic,
            MessageCount = settings.MessageCount, Runs = settings.ConsumerRuns
        }
    ];
}
