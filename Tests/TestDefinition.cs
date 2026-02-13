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
/// DeliveryHandler = fire-and-forget Produce with delivery handler callback.
/// RequestResponse = ProduceAsync + await each message individually.
/// ConcurrencyWindow = fire N ProduceAsync calls, await all with Task.WhenAll.
/// </summary>
public enum CommitStrategy { DeliveryHandler, RequestResponse, ConcurrencyWindow, ManualPerMessage, ManualBatch }

/// <summary>SpecificRecord for Avro; NotApplicable for JSON.</summary>
public enum RecordType { SpecificRecord, NotApplicable }

/// <summary>
/// Represents a single test in the benchmark matrix.
///
/// Scenario-based producer tests:
///   T1.x Fire-and-Forget:  Produce + delivery handler callback
///   T2.x Request-Response: ProduceAsync + await each message
///   T3.x Batch Processing: ProduceAsync + Task.WhenAll concurrency windows (1, 10, 100)
///   T3B.x Business-Realistic Batch: same as T3.x but with inter-message delay
///
/// All producer tests use acks=all + enable.idempotence=true.
/// Avro uses SpecificRecord only.
///
/// Consumer tests (T4.1–T4.8) read from pre-populated topics.
///
/// Total: 40 tests (4 + 4 + 12 + 12 + 8).
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
    public int CommitBatchSize { get; init; }
    public int CommitIntervalMs { get; init; }
    public int InterMessageDelayMs { get; init; }

    /// <summary>
    /// Builds the full test matrix from the provided settings.
    /// T1.x fire-and-forget (4 tests), T2.x request-response (4 tests),
    /// T3.x batch processing (12 tests), T3B.x business-realistic batch (12 tests),
    /// T4.x consumer (8 tests) = 40 tests total.
    /// </summary>
    public static List<TestDefinition> GetAll(Config.TestSettings settings)
    {
        TimeSpan? duration = settings.DurationMinutes.HasValue
            ? TimeSpan.FromMinutes(settings.DurationMinutes.Value)
            : null;

        var producerRuns = settings.ProducerRuns;
        var consumerRuns = settings.ConsumerRuns;

        var tests = new List<TestDefinition>();

        // ── T1.x Fire-and-Forget (Produce + delivery handler) ────────────
        int t1Num = 1;
        foreach (var format in new[] { SerializationFormat.Avro, SerializationFormat.Json })
        {
            foreach (var size in new[] { PayloadSize.Small, PayloadSize.Large })
            {
                var recordType = format == SerializationFormat.Avro
                    ? RecordType.SpecificRecord
                    : RecordType.NotApplicable;

                string topic = GetProducerTopic(settings, format, size);

                tests.Add(new TestDefinition
                {
                    Id = $"T1.{t1Num}",
                    Name = BuildFireAndForgetName(format, size),
                    Type = TestType.Producer,
                    Format = format,
                    Size = size,
                    RecordType = recordType,
                    ProduceApi = ProduceApi.Produce,
                    CommitStrategy = CommitStrategy.DeliveryHandler,
                    Topic = topic,
                    MessageCount = settings.MessageCount,
                    Duration = duration,
                    Runs = producerRuns,
                });
                t1Num++;
            }
        }

        // ── T2.x Request-Response (ProduceAsync + await each) ────────────
        int t2Num = 1;
        foreach (var format in new[] { SerializationFormat.Avro, SerializationFormat.Json })
        {
            foreach (var size in new[] { PayloadSize.Small, PayloadSize.Large })
            {
                var recordType = format == SerializationFormat.Avro
                    ? RecordType.SpecificRecord
                    : RecordType.NotApplicable;

                string topic = GetProducerTopic(settings, format, size);

                tests.Add(new TestDefinition
                {
                    Id = $"T2.{t2Num}",
                    Name = BuildRequestResponseName(format, size),
                    Type = TestType.Producer,
                    Format = format,
                    Size = size,
                    RecordType = recordType,
                    ProduceApi = ProduceApi.ProduceAsync,
                    CommitStrategy = CommitStrategy.RequestResponse,
                    Topic = topic,
                    MessageCount = settings.MessageCount,
                    Duration = duration,
                    Runs = producerRuns,
                });
                t2Num++;
            }
        }

        // ── T3.x Batch Processing (ProduceAsync + Task.WhenAll) ──────────
        var windows = new[] { 1, 10, 100 };
        int t3Num = 1;

        foreach (var format in new[] { SerializationFormat.Avro, SerializationFormat.Json })
        {
            foreach (var size in new[] { PayloadSize.Small, PayloadSize.Large })
            {
                var recordType = format == SerializationFormat.Avro
                    ? RecordType.SpecificRecord
                    : RecordType.NotApplicable;

                string topic = GetProducerTopic(settings, format, size);

                foreach (var window in windows)
                {
                    tests.Add(new TestDefinition
                    {
                        Id = $"T3.{t3Num}",
                        Name = BuildBatchName(format, size, window),
                        Type = TestType.Producer,
                        Format = format,
                        Size = size,
                        RecordType = recordType,
                        ProduceApi = ProduceApi.ProduceAsync,
                        CommitStrategy = CommitStrategy.ConcurrencyWindow,
                        Topic = topic,
                        MessageCount = settings.MessageCount,
                        Duration = duration,
                        Runs = producerRuns,
                        ConcurrencyWindow = window,
                    });
                    t3Num++;
                }
            }
        }

        // ── T3B.x Business-Realistic Batch (ProduceAsync + Task.WhenAll + inter-message delay)
        int t3bNum = 1;
        foreach (var format in new[] { SerializationFormat.Avro, SerializationFormat.Json })
        {
            foreach (var size in new[] { PayloadSize.Small, PayloadSize.Large })
            {
                var recordType = format == SerializationFormat.Avro
                    ? RecordType.SpecificRecord
                    : RecordType.NotApplicable;
                string topic = GetProducerTopic(settings, format, size);
                foreach (var window in windows)
                {
                    tests.Add(new TestDefinition
                    {
                        Id = $"T3B.{t3bNum}",
                        Name = BuildBatchRealisticName(format, size, window),
                        Type = TestType.Producer,
                        Format = format,
                        Size = size,
                        RecordType = recordType,
                        ProduceApi = ProduceApi.ProduceAsync,
                        CommitStrategy = CommitStrategy.ConcurrencyWindow,
                        Topic = topic,
                        MessageCount = settings.MessageCount,
                        Duration = duration,
                        Runs = producerRuns,
                        ConcurrencyWindow = window,
                        InterMessageDelayMs = settings.InterMessageDelayMs,
                    });
                    t3bNum++;
                }
            }
        }

        // ── T4.x Consumer tests (2 commit modes × 4 format/size combos) ─
        var commitModes = new[]
        {
            (Strategy: CommitStrategy.ManualPerMessage, Label: "PerMsg",  BatchSize: 0,                       IntervalMs: 0),
            (Strategy: CommitStrategy.ManualBatch,      Label: "Batch",   BatchSize: settings.CommitBatchSize, IntervalMs: settings.CommitIntervalMs),
        };

        int t4Num = 1;
        foreach (var mode in commitModes)
        {
            foreach (var format in new[] { SerializationFormat.Avro, SerializationFormat.Json })
            {
                foreach (var size in new[] { PayloadSize.Small, PayloadSize.Large })
                {
                    string topic = GetConsumerTopic(settings, format, size);

                    tests.Add(new TestDefinition
                    {
                        Id = $"T4.{t4Num}",
                        Name = BuildConsumerName(format, size, mode.Label),
                        Type = TestType.Consumer,
                        Format = format,
                        Size = size,
                        RecordType = RecordType.NotApplicable,
                        ProduceApi = ProduceApi.Produce,
                        CommitStrategy = mode.Strategy,
                        Topic = topic,
                        MessageCount = settings.MessageCount,
                        Duration = duration,
                        Runs = consumerRuns,
                        CommitBatchSize = mode.BatchSize,
                        CommitIntervalMs = mode.IntervalMs,
                    });
                    t4Num++;
                }
            }
        }

        return tests;
    }

    private static string GetProducerTopic(
        Config.TestSettings settings, SerializationFormat format, PayloadSize size)
    {
        return (format, size) switch
        {
            (SerializationFormat.Avro, PayloadSize.Small) => settings.AvroSmallSpecificTopic,
            (SerializationFormat.Avro, PayloadSize.Large) => settings.AvroLargeSpecificTopic,
            (SerializationFormat.Json, PayloadSize.Small) => settings.JsonSmallTopic,
            (SerializationFormat.Json, PayloadSize.Large) => settings.JsonLargeTopic,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static string GetConsumerTopic(
        Config.TestSettings settings, SerializationFormat format, PayloadSize size)
    {
        return (format, size) switch
        {
            (SerializationFormat.Avro, PayloadSize.Small) => settings.AvroSmallSpecificTopic,
            (SerializationFormat.Avro, PayloadSize.Large) => settings.AvroLargeSpecificTopic,
            (SerializationFormat.Json, PayloadSize.Small) => settings.JsonSmallTopic,
            (SerializationFormat.Json, PayloadSize.Large) => settings.JsonLargeTopic,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static string BuildConsumerName(SerializationFormat format, PayloadSize size, string modeLabel)
    {
        return $"Consumer {format} {size} {modeLabel}";
    }

    private static string BuildFireAndForgetName(SerializationFormat format, PayloadSize size)
    {
        return $"Fire-and-Forget {format} {size}";
    }

    private static string BuildRequestResponseName(SerializationFormat format, PayloadSize size)
    {
        return $"Request-Response {format} {size}";
    }

    private static string BuildBatchName(SerializationFormat format, PayloadSize size, int window)
    {
        return $"Batch {format} {size} Window-{window}";
    }

    private static string BuildBatchRealisticName(SerializationFormat format, PayloadSize size, int window)
    {
        return $"Batch-Realistic {format} {size} Window-{window}";
    }
}
