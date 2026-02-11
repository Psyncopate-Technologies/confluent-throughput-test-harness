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
public enum CommitStrategy { DeliveryHandler, RequestResponse, ConcurrencyWindow }

/// <summary>SpecificRecord for Avro; NotApplicable for JSON.</summary>
public enum RecordType { SpecificRecord, NotApplicable }

/// <summary>
/// Represents a single test in the benchmark matrix.
///
/// Scenario-based producer tests:
///   T1.x Fire-and-Forget:  Produce + delivery handler callback
///   T2.x Request-Response: ProduceAsync + await each message
///   T3.x Batch Processing: ProduceAsync + Task.WhenAll concurrency windows (10, 100)
///
/// All producer tests use acks=all + enable.idempotence=true.
/// Avro uses SpecificRecord only.
///
/// Consumer tests (T4.1–T4.4) read from pre-populated topics.
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
    /// T1.x fire-and-forget (4 tests), T2.x request-response (4 tests),
    /// T3.x batch processing (8 tests), T4.x consumer (4 tests) = 20 tests total.
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
        var windows = new[] { 10, 100 };
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

        // ── T4.x Consumer tests ──────────────────────────────────────────
        var consumerTests = new List<TestDefinition>
        {
            new()
            {
                Id = "T4.1", Name = "Consumer Avro Small",
                Type = TestType.Consumer, Format = SerializationFormat.Avro,
                Size = PayloadSize.Small, RecordType = RecordType.NotApplicable,
                ProduceApi = ProduceApi.Produce, CommitStrategy = CommitStrategy.DeliveryHandler,
                Topic = settings.AvroSmallTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
            new()
            {
                Id = "T4.2", Name = "Consumer Avro Large",
                Type = TestType.Consumer, Format = SerializationFormat.Avro,
                Size = PayloadSize.Large, RecordType = RecordType.NotApplicable,
                ProduceApi = ProduceApi.Produce, CommitStrategy = CommitStrategy.DeliveryHandler,
                Topic = settings.AvroLargeTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
            new()
            {
                Id = "T4.3", Name = "Consumer JSON Small",
                Type = TestType.Consumer, Format = SerializationFormat.Json,
                Size = PayloadSize.Small, RecordType = RecordType.NotApplicable,
                ProduceApi = ProduceApi.Produce, CommitStrategy = CommitStrategy.DeliveryHandler,
                Topic = settings.JsonSmallTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
            new()
            {
                Id = "T4.4", Name = "Consumer JSON Large",
                Type = TestType.Consumer, Format = SerializationFormat.Json,
                Size = PayloadSize.Large, RecordType = RecordType.NotApplicable,
                ProduceApi = ProduceApi.Produce, CommitStrategy = CommitStrategy.DeliveryHandler,
                Topic = settings.JsonLargeTopic,
                MessageCount = settings.MessageCount, Duration = duration,
                Runs = consumerRuns
            },
        };

        tests.AddRange(consumerTests);
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
}
