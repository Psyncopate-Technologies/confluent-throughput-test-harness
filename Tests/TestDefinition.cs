// ────────────────────────────────────────────────────────────────────
// TestDefinition.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Defines the test matrix enums (format, payload size, type)
//           and the TestDefinition class that maps each test ID to its
//           topic, serialization format, runs, and message count.
// ────────────────────────────────────────────────────────────────────

namespace ConfluentThroughputTestHarness.Tests;

// ── Enums used to classify each test in the matrix ──────────────────

/// <summary>Avro (binary, schema-aware) vs Json (text, JSON Schema-aware).</summary>
public enum SerializationFormat { Avro, Json }

/// <summary>Small = 25-field freight subset, Large = full 104-field freight table.</summary>
public enum PayloadSize { Small, Large }

/// <summary>Producer writes messages to Kafka; Consumer reads them back.</summary>
public enum TestType { Producer, Consumer }

/// <summary>
/// Represents a single test in the 8-test benchmark matrix.
///
/// The test matrix covers every combination of:
///   - Direction: Producer (T1.x) or Consumer (T2.x)
///   - Format:    Avro or JSON
///   - Size:      Small (25 fields) or Large (104 fields)
///
/// Each test targets a specific topic and runs a configurable number of
/// iterations (default: 3 for producers, 5 for consumers).
/// </summary>
public class TestDefinition
{
    public string Id { get; init; } = string.Empty;           // e.g., "T1.1", "T2.3"
    public string Name { get; init; } = string.Empty;         // e.g., "Producer Avro Small"
    public TestType Type { get; init; }                       // Producer or Consumer
    public SerializationFormat Format { get; init; }          // Avro or Json
    public PayloadSize Size { get; init; }                    // Small or Large
    public string Topic { get; init; } = string.Empty;        // Kafka topic name
    public int MessageCount { get; init; }                    // Messages per run (count mode)
    public TimeSpan? Duration { get; init; }                  // Duration per run (duration mode, overrides MessageCount)
    public int Runs { get; init; }                            // Number of runs per test

    /// <summary>
    /// Builds the full 8-test matrix from the provided settings.
    /// Producer tests (T1.1-T1.4) run first and populate the topics;
    /// Consumer tests (T2.1-T2.4) then read from those topics.
    /// </summary>
    public static List<TestDefinition> GetAll(Config.TestSettings settings)
    {
        TimeSpan? duration = settings.DurationMinutes.HasValue
            ? TimeSpan.FromMinutes(settings.DurationMinutes.Value)
            : null;

        // In duration mode, default to 1 run per test since each run already
        // covers a sustained period (e.g., 10 or 15 minutes). In count mode,
        // use the configured ProducerRuns/ConsumerRuns (default 3/5).
        var producerRuns = duration.HasValue ? 1 : settings.ProducerRuns;
        var consumerRuns = duration.HasValue ? 1 : settings.ConsumerRuns;

        return
        [
            // ── Producer tests (T1.x) ──────────────────────────────────
            // These run first and write messages to the topics.

            new()   // T1.1: Produce small Avro messages
            {
                Id = "T1.1", Name = "Producer Avro Small",
                Type = TestType.Producer, Format = SerializationFormat.Avro,
                Size = PayloadSize.Small, Topic = settings.AvroSmallTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = producerRuns
            },
            new()   // T1.2: Produce large Avro messages
            {
                Id = "T1.2", Name = "Producer Avro Large",
                Type = TestType.Producer, Format = SerializationFormat.Avro,
                Size = PayloadSize.Large, Topic = settings.AvroLargeTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = producerRuns
            },
            new()   // T1.3: Produce small JSON messages
            {
                Id = "T1.3", Name = "Producer JSON Small",
                Type = TestType.Producer, Format = SerializationFormat.Json,
                Size = PayloadSize.Small, Topic = settings.JsonSmallTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = producerRuns
            },
            new()   // T1.4: Produce large JSON messages
            {
                Id = "T1.4", Name = "Producer JSON Large",
                Type = TestType.Producer, Format = SerializationFormat.Json,
                Size = PayloadSize.Large, Topic = settings.JsonLargeTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = producerRuns
            },

            // ── Consumer tests (T2.x) ──────────────────────────────────
            // These read from the topics populated by the producer tests above.
            // Each run uses a unique consumer group to read from offset 0.

            new()   // T2.1: Consume small Avro messages
            {
                Id = "T2.1", Name = "Consumer Avro Small",
                Type = TestType.Consumer, Format = SerializationFormat.Avro,
                Size = PayloadSize.Small, Topic = settings.AvroSmallTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
            new()   // T2.2: Consume large Avro messages
            {
                Id = "T2.2", Name = "Consumer Avro Large",
                Type = TestType.Consumer, Format = SerializationFormat.Avro,
                Size = PayloadSize.Large, Topic = settings.AvroLargeTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
            new()   // T2.3: Consume small JSON messages
            {
                Id = "T2.3", Name = "Consumer JSON Small",
                Type = TestType.Consumer, Format = SerializationFormat.Json,
                Size = PayloadSize.Small, Topic = settings.JsonSmallTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
            new()   // T2.4: Consume large JSON messages
            {
                Id = "T2.4", Name = "Consumer JSON Large",
                Type = TestType.Consumer, Format = SerializationFormat.Json,
                Size = PayloadSize.Large, Topic = settings.JsonLargeTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            }
        ];
    }
}
