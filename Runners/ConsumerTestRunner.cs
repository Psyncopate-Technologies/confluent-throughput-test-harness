// ────────────────────────────────────────────────────────────────────
// ConsumerTestRunner.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Executes consumer throughput benchmarks for both Avro and
//           JSON serialization formats. Consumes messages using unique
//           consumer groups per run and collects timing, byte count,
//           and resource metrics via ByteCountingDeserializer.
// ────────────────────────────────────────────────────────────────────

using System.Diagnostics;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using ConfluentThroughputTestHarness.Config;
using ConfluentThroughputTestHarness.Metrics;
using ConfluentThroughputTestHarness.Models;
using ConfluentThroughputTestHarness.Tests;

namespace ConfluentThroughputTestHarness.Runners;

public class ConsumerTestRunner
{
    private readonly KafkaSettings _kafkaSettings;
    private readonly SchemaRegistrySettings _srSettings;

    public ConsumerTestRunner(KafkaSettings kafkaSettings, SchemaRegistrySettings srSettings)
    {
        _kafkaSettings = kafkaSettings;
        _srSettings = srSettings;
    }

    /// <summary>
    /// Entry point for a single consumer benchmark run.
    /// Routes to the Avro or JSON code path based on the test definition's format.
    /// </summary>
    /// <summary>
    /// The optional onProgress callback is invoked approximately every second with
    /// the current message count and elapsed time, enabling live progress display.
    /// </summary>
    public async Task<TestResult> RunAsync(TestDefinition test, int runNumber,
        Action<int, TimeSpan>? onProgress = null)
    {
        return test.Format switch
        {
            SerializationFormat.Avro => await RunAvroAsync(test, runNumber, onProgress),
            SerializationFormat.Json => await RunJsonAsync(test, runNumber, onProgress),
            _ => throw new ArgumentException($"Unknown format: {test.Format}")
        };
    }

    /// <summary>
    /// Consumes Avro-serialized messages from the test topic.
    ///
    /// Key design points:
    /// - Keys are deserialized with AvroDeserializer&lt;int&gt; (matching the producer's
    ///   AvroSerializer&lt;int&gt;), wrapped with .AsSyncOverAsync().
    /// - Values are deserialized with AvroDeserializer&lt;GenericRecord&gt;, wrapped in a
    ///   ByteCountingAsyncDeserializer to capture raw message byte sizes before
    ///   deserialization occurs. This enables accurate MB/sec throughput metrics.
    /// - Each run gets a unique consumer group ID (with a GUID suffix) to ensure
    ///   it reads the full topic from offset 0.
    /// </summary>
    private Task<TestResult> RunAvroAsync(TestDefinition test, int runNumber,
        Action<int, TimeSpan>? onProgress = null)
    {
        var consumerConfig = BuildConsumerConfig(test.Id, runNumber);
        var schemaRegistryConfig = BuildSchemaRegistryConfig();

        using var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);

        // Wrap the Avro value deserializer in ByteCountingAsyncDeserializer to track
        // the total raw bytes consumed (before deserialization into GenericRecord).
        var avroDeserializer = new AvroDeserializer<GenericRecord>(schemaRegistry);
        var byteCounter = new ByteCountingAsyncDeserializer<GenericRecord>(avroDeserializer);

        using var consumer = new ConsumerBuilder<int, GenericRecord>(consumerConfig)
            .SetKeyDeserializer(new AvroDeserializer<int>(schemaRegistry).AsSyncOverAsync())
            .SetValueDeserializer(byteCounter.AsSyncOverAsync())
            .SetErrorHandler((_, e) => Console.Error.WriteLine($"Consumer error: {e.Reason}"))
            .Build();

        return Task.FromResult(ConsumeMessages(consumer, test, runNumber, () => byteCounter.TotalBytes, onProgress));
    }

    /// <summary>
    /// Consumes JSON-serialized messages from the test topic.
    /// Routes to the generic RunJsonTypedAsync&lt;T&gt; with the appropriate POCO type.
    /// </summary>
    private Task<TestResult> RunJsonAsync(TestDefinition test, int runNumber,
        Action<int, TimeSpan>? onProgress = null)
    {
        if (test.Size == PayloadSize.Small)
            return RunJsonTypedAsync<FreightDboTblLoadsSmall>(test, runNumber, onProgress);
        else
            return RunJsonTypedAsync<FreightDboTblLoads>(test, runNumber, onProgress);
    }

    /// <summary>
    /// Generic JSON consumer benchmark. The type parameter T is the POCO class
    /// matching the JSON schema.
    ///
    /// Key differences from the Avro path:
    /// - Uses JsonDeserializer&lt;T&gt; (no Schema Registry client needed for deserialization).
    /// - Keys use the default Deserializers.Int32 (matching the producer's Serializers.Int32).
    /// - The byte counter wraps JsonDeserializer to track raw bytes before JSON parsing.
    /// </summary>
    private Task<TestResult> RunJsonTypedAsync<T>(TestDefinition test, int runNumber,
        Action<int, TimeSpan>? onProgress = null) where T : class
    {
        var consumerConfig = BuildConsumerConfig(test.Id, runNumber);
        var schemaRegistryConfig = BuildSchemaRegistryConfig();

        using var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);
        var jsonDeserializer = new JsonDeserializer<T>();
        var byteCounter = new ByteCountingAsyncDeserializer<T>(jsonDeserializer);

        // Note: no SetKeyDeserializer call -- the default Deserializers.Int32 handles int keys
        using var consumer = new ConsumerBuilder<int, T>(consumerConfig)
            .SetValueDeserializer(byteCounter.AsSyncOverAsync())
            .SetErrorHandler((_, e) => Console.Error.WriteLine($"Consumer error: {e.Reason}"))
            .Build();

        return Task.FromResult(ConsumeMessages(consumer, test, runNumber, () => byteCounter.TotalBytes, onProgress));
    }

    /// <summary>
    /// Core consume loop shared by both Avro and JSON paths.
    ///
    /// Subscribes to the test topic and polls messages until the termination condition
    /// is met. Supports two modes:
    ///   Count-based: consume until messagesConsumed reaches test.MessageCount.
    ///   Duration-based: consume until elapsed time reaches test.Duration.
    ///
    /// Uses a 5-second timeout per Consume() call. In count mode, a timeout aborts
    /// the run (likely means the topic has fewer messages than expected). In duration
    /// mode, timeouts are expected when the topic is temporarily drained and the loop
    /// continues until the time limit expires.
    ///
    /// Offset commits are manual (EnableAutoCommit = false):
    ///   ManualPerMessage: consumer.Commit(result) after every message.
    ///   ManualBatch: consumer.Commit() every CommitBatchSize messages OR when
    ///     CommitIntervalMs has elapsed since the last commit, whichever comes first.
    /// A final consumer.Commit() runs before Close() to flush any stragglers.
    ///
    /// Partition EOF events are skipped (they are informational, not errors).
    /// ConsumeExceptions are counted as errors; if errors exceed 100, the run is
    /// aborted to prevent infinite error loops.
    ///
    /// The getBytesConsumed delegate returns the running total from the
    /// ByteCountingAsyncDeserializer wrapper, which captures raw byte sizes before
    /// deserialization.
    /// </summary>
    private TestResult ConsumeMessages<TValue>(
        IConsumer<int, TValue> consumer,
        TestDefinition test,
        int runNumber,
        Func<long> getBytesConsumed,
        Action<int, TimeSpan>? onProgress = null)
    {
        consumer.Subscribe(test.Topic);

        using var monitor = new ResourceMonitor();
        var messagesConsumed = 0;
        var errors = 0;
        var timeout = TimeSpan.FromSeconds(5);
        var isDurationMode = test.Duration.HasValue;
        var lastProgressReport = TimeSpan.Zero;

        var sw = Stopwatch.StartNew();
        var commitTimer = Stopwatch.StartNew();

        // Hybrid loop: stop when either message count or duration limit is reached (whichever first)
        while (messagesConsumed < test.MessageCount
            && (!isDurationMode || sw.Elapsed < test.Duration!.Value))
        {
            try
            {
                var result = consumer.Consume(timeout);

                // Null result means the timeout elapsed with no message available
                if (result == null)
                {
                    if (!isDurationMode)
                    {
                        Console.WriteLine($"  [WARNING] Timeout waiting for messages at {messagesConsumed}/{test.MessageCount}");
                        break;
                    }
                    // In duration mode, report progress even on timeout (time is still advancing)
                    if (onProgress != null && sw.Elapsed - lastProgressReport >= TimeSpan.FromSeconds(1))
                    {
                        onProgress(messagesConsumed, sw.Elapsed);
                        lastProgressReport = sw.Elapsed;
                    }
                    continue;
                }

                // Partition EOF is an informational event (not a real message), skip it.
                // This fires when the consumer reaches the end of the partition's log.
                if (result.IsPartitionEOF)
                {
                    continue;
                }

                messagesConsumed++;

                // Manual offset commit based on the test's commit strategy
                if (test.CommitStrategy == CommitStrategy.ManualPerMessage)
                {
                    consumer.Commit(result);
                }
                else if (test.CommitStrategy == CommitStrategy.ManualBatch)
                {
                    bool countThreshold = test.CommitBatchSize > 0
                        && messagesConsumed % test.CommitBatchSize == 0;
                    bool timeThreshold = test.CommitIntervalMs > 0
                        && commitTimer.ElapsedMilliseconds >= test.CommitIntervalMs;

                    if (countThreshold || timeThreshold)
                    {
                        consumer.Commit();
                        commitTimer.Restart();
                    }
                }

                // Report progress approximately every second for live UI updates
                if (onProgress != null && sw.Elapsed - lastProgressReport >= TimeSpan.FromSeconds(1))
                {
                    onProgress(messagesConsumed, sw.Elapsed);
                    lastProgressReport = sw.Elapsed;
                }
            }
            catch (ConsumeException ex)
            {
                errors++;
                if (errors > 100)
                {
                    Console.Error.WriteLine($"  [ERROR] Too many consume errors, aborting. Last: {ex.Error.Reason}");
                    break;
                }
            }
        }

        sw.Stop();

        // Commit any remaining uncommitted offsets before leaving the group
        try { consumer.Commit(); } catch (KafkaException) { }

        consumer.Close();   // Gracefully leave the consumer group

        return new TestResult
        {
            TestId = test.Id,
            TestName = test.Name,
            RunNumber = runNumber,
            MessageCount = messagesConsumed,
            TotalBytes = getBytesConsumed(),
            Elapsed = sw.Elapsed,
            ProduceApi = test.ProduceApi.ToString(),
            CommitStrategy = test.CommitStrategy.ToString(),
            PeakCpuPercent = monitor.PeakCpuPercent,
            PeakMemoryBytes = monitor.PeakMemoryBytes,
            DeliveryErrors = errors
        };
    }

    /// <summary>
    /// Builds the consumer configuration for a single benchmark run.
    ///
    /// Key settings:
    /// - GroupId includes the test ID, run number, and a GUID to ensure each run
    ///   gets a brand new consumer group that reads from offset 0 (Earliest).
    /// - EnablePartitionEof=true so the consume loop can detect when it reaches
    ///   the end of the topic (fires IsPartitionEOF on the ConsumeResult).
    /// - FetchMaxBytes=50MB and MaxPartitionFetchBytes=10MB are set high to allow
    ///   large batches for throughput testing.
    /// </summary>
    private ConsumerConfig BuildConsumerConfig(string testId, int runNumber)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            SecurityProtocol = Enum.Parse<SecurityProtocol>(_kafkaSettings.SecurityProtocol, true),
            SaslMechanism = Enum.Parse<SaslMechanism>(_kafkaSettings.SaslMechanism, true),
            SaslUsername = _kafkaSettings.SaslUsername,
            SaslPassword = _kafkaSettings.SaslPassword,
            GroupId = $"throughput-test-{testId}-run-{runNumber}-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            FetchMinBytes = 1,
            FetchMaxBytes = 52_428_800, // 50MB
            MaxPartitionFetchBytes = 10_485_760, // 10MB
            EnablePartitionEof = true
        };
        // Suppress rdkafka informational logs (e.g., telemetry instance id changes)
        config.Set("log_level", "3"); // 3 = error
        return config;
    }

    /// <summary>
    /// Builds the Schema Registry client configuration for authenticating
    /// with Confluent Cloud Schema Registry using API key/secret.
    /// </summary>
    private SchemaRegistryConfig BuildSchemaRegistryConfig() => new()
    {
        Url = _srSettings.Url,
        BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo,
        BasicAuthUserInfo = _srSettings.BasicAuthUserInfo
    };
}
