// ────────────────────────────────────────────────────────────────────
// TestDefinition.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Defines the test matrix enums (format, payload size, type,
//           produce API, commit strategy, record type) and the
//           TestDefinition class that maps each test ID to its
//           topic, serialization format, runs, and message count.
// ────────────────────────────────────────────────────────────────────

namespace ConfluentThroughputTestHarness.Tests;

// ── Enums used to classify each test in the matrix ──────────────────

/// <summary>Avro (binary, schema-aware) vs Json (text, JSON Schema-aware).</summary>
public enum SerializationFormat { Avro, Json }

/// <summary>Small = 27-field freight subset, Large = full 106-field freight table.</summary>
public enum PayloadSize { Small, Large }

/// <summary>Producer writes messages to Kafka; Consumer reads them back.</summary>
public enum TestType { Producer, Consumer }

/// <summary>Produce (sync fire-and-forget w/ delivery handler) vs ProduceAsync (await each).</summary>
public enum ProduceApi { Produce, ProduceAsync }

/// <summary>
/// Single = flush after every message.
/// BatchConfigurable = flush every BatchCommitSize messages (from config).
/// Batch5K = flush every 5,000 messages (hardcoded).
/// ConcurrencyWindow = fire N ProduceAsync calls, await all with Task.WhenAll.
/// </summary>
public enum CommitStrategy { Single, BatchConfigurable, Batch5K, ConcurrencyWindow }

/// <summary>GenericRecord or SpecificRecord for Avro; NotApplicable for JSON.</summary>
public enum RecordType { GenericRecord, SpecificRecord, NotApplicable }

/// <summary>
/// Represents a single test in the benchmark matrix.
///
/// Drag-race producer tests (T1.1–T1.8):
///   - Serialization: Avro or JSON
///   - Payload Size:  Small (27 fields) or Large (106 fields)
///   - Record Type:   SpecificRecord (Avro) or N/A (JSON)
///   - Produce API:   Produce (fire-and-forget) or ProduceAsync (await each)
///   - Commit Strategy: Single (flush every message)
///
/// Business-realistic producer tests (T3.1–T3.16):
///   - ProduceAsync + acks=all + enable.idempotence=true
///   - Task.WhenAll concurrency windows: 1, 10, 50, 100
///   - 4 format/size combos × 4 windows = 16 tests
///
/// Consumer tests (T2.1–T2.4) remain unchanged.
/// </summary>
public class TestDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TestType Type { get; init; }
    public SerializationFormat Format { get; init; }
    public PayloadSize Size { get; init; }
    public RecordType RecordType { get; init; }
    public ProduceApi ProduceApi { get; init; }
    public CommitStrategy CommitStrategy { get; init; }
    public string Topic { get; init; } = string.Empty;
    public int MessageCount { get; init; }
    public TimeSpan? Duration { get; init; }
    public int Runs { get; init; }
    public int ConcurrencyWindow { get; init; }

    /// <summary>
    /// Builds the full test matrix from the provided settings.
    /// Drag-race producer tests (T1.1–T1.8) run first and populate the topics;
    /// Business-realistic tests (T3.1–T3.16) use acks=all + idempotence + concurrency windows;
    /// Consumer tests (T2.1–T2.4) then read from those topics.
    /// </summary>
    public static List<TestDefinition> GetAll(Config.TestSettings settings)
    {
        TimeSpan? duration = settings.DurationMinutes.HasValue
            ? TimeSpan.FromMinutes(settings.DurationMinutes.Value)
            : null;

        var producerRuns = settings.ProducerRuns;
        var businessRuns = settings.BusinessRealisticRuns;
        var consumerRuns = settings.ConsumerRuns;

        // ── Drag-race producer tests (T1.1–T1.8) ────────────────────────
        // Produce and ProduceAsync with Single commit only. Avro uses SpecificRecord only.
        var apiCombos = new[]
        {
            ProduceApi.Produce,
            ProduceApi.ProduceAsync,
        };

        var producerTests = new List<TestDefinition>();
        int testNum = 1;

        foreach (var format in new[] { SerializationFormat.Avro, SerializationFormat.Json })
        {
            foreach (var size in new[] { PayloadSize.Small, PayloadSize.Large })
            {
                var recordType = format == SerializationFormat.Avro
                    ? RecordType.SpecificRecord
                    : RecordType.NotApplicable;

                string topic = GetProducerTopic(settings, format, size, recordType);

                foreach (var api in apiCombos)
                {
                    string name = BuildProducerName(format, size, recordType, api, CommitStrategy.Single);

                    producerTests.Add(new TestDefinition
                    {
                        Id = $"T1.{testNum}",
                        Name = name,
                        Type = TestType.Producer,
                        Format = format,
                        Size = size,
                        RecordType = recordType,
                        ProduceApi = api,
                        CommitStrategy = CommitStrategy.Single,
                        Topic = topic,
                        MessageCount = settings.MessageCount,
                        Duration = duration,
                        Runs = producerRuns,
                    });
                    testNum++;
                }
            }
        }

        // ── Business-realistic producer tests (T3.1–T3.16) ──────────────
        // ProduceAsync + acks=all + idempotence + Task.WhenAll concurrency windows.
        var windows = new[] { 1, 10, 50, 100 };
        int t3Num = 1;

        foreach (var format in new[] { SerializationFormat.Avro, SerializationFormat.Json })
        {
            foreach (var size in new[] { PayloadSize.Small, PayloadSize.Large })
            {
                var recordType = format == SerializationFormat.Avro
                    ? RecordType.SpecificRecord
                    : RecordType.NotApplicable;

                string topic = GetProducerTopic(settings, format, size, recordType);

                foreach (var window in windows)
                {
                    string name = BuildBusinessName(format, size, window);

                    producerTests.Add(new TestDefinition
                    {
                        Id = $"T3.{t3Num}",
                        Name = name,
                        Type = TestType.Producer,
                        Format = format,
                        Size = size,
                        RecordType = recordType,
                        ProduceApi = ProduceApi.ProduceAsync,
                        CommitStrategy = CommitStrategy.ConcurrencyWindow,
                        Topic = topic,
                        MessageCount = settings.MessageCount,
                        Duration = duration,
                        Runs = businessRuns,
                        ConcurrencyWindow = window,
                    });
                    t3Num++;
                }
            }
        }

        // ── Consumer tests (T2.x) ──────────────────────────────────────
        // Unchanged — read from the topics populated by producer tests.
        var consumerTests = new List<TestDefinition>
        {
            new()
            {
                Id = "T2.1", Name = "Consumer Avro Small",
                Type = TestType.Consumer, Format = SerializationFormat.Avro,
                Size = PayloadSize.Small, RecordType = RecordType.NotApplicable,
                ProduceApi = ProduceApi.Produce, CommitStrategy = CommitStrategy.Single,
                Topic = settings.AvroSmallTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
            new()
            {
                Id = "T2.2", Name = "Consumer Avro Large",
                Type = TestType.Consumer, Format = SerializationFormat.Avro,
                Size = PayloadSize.Large, RecordType = RecordType.NotApplicable,
                ProduceApi = ProduceApi.Produce, CommitStrategy = CommitStrategy.Single,
                Topic = settings.AvroLargeTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
            new()
            {
                Id = "T2.3", Name = "Consumer JSON Small",
                Type = TestType.Consumer, Format = SerializationFormat.Json,
                Size = PayloadSize.Small, RecordType = RecordType.NotApplicable,
                ProduceApi = ProduceApi.Produce, CommitStrategy = CommitStrategy.Single,
                Topic = settings.JsonSmallTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
            new()
            {
                Id = "T2.4", Name = "Consumer JSON Large",
                Type = TestType.Consumer, Format = SerializationFormat.Json,
                Size = PayloadSize.Large, RecordType = RecordType.NotApplicable,
                ProduceApi = ProduceApi.Produce, CommitStrategy = CommitStrategy.Single,
                Topic = settings.JsonLargeTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
        };

        producerTests.AddRange(consumerTests);
        return producerTests;
    }

    private static string GetProducerTopic(
        Config.TestSettings settings, SerializationFormat format,
        PayloadSize size, RecordType recordType)
    {
        return (format, size, recordType) switch
        {
            (SerializationFormat.Avro, PayloadSize.Small, RecordType.SpecificRecord) => settings.AvroSmallSpecificTopic,
            (SerializationFormat.Avro, PayloadSize.Small, RecordType.GenericRecord)  => settings.AvroSmallGenericTopic,
            (SerializationFormat.Avro, PayloadSize.Large, RecordType.SpecificRecord) => settings.AvroLargeSpecificTopic,
            (SerializationFormat.Avro, PayloadSize.Large, RecordType.GenericRecord)  => settings.AvroLargeGenericTopic,
            (SerializationFormat.Json, PayloadSize.Small, _) => settings.JsonSmallTopic,
            (SerializationFormat.Json, PayloadSize.Large, _) => settings.JsonLargeTopic,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static string BuildProducerName(
        SerializationFormat format, PayloadSize size, RecordType recordType,
        ProduceApi api, CommitStrategy strategy)
    {
        string recordLabel = recordType switch
        {
            RecordType.SpecificRecord => " Specific",
            RecordType.GenericRecord  => " Generic",
            _ => ""
        };

        string strategyLabel = strategy switch
        {
            CommitStrategy.Single            => "Single",
            CommitStrategy.BatchConfigurable => "BatchConfig",
            CommitStrategy.Batch5K           => "Batch5K",
            CommitStrategy.ConcurrencyWindow => "Window",
            _ => strategy.ToString()
        };

        return $"Producer {format} {size}{recordLabel} {api} {strategyLabel}";
    }

    private static string BuildBusinessName(
        SerializationFormat format, PayloadSize size, int window)
    {
        return $"Business {format} {size} Window-{window}";
    }
}
