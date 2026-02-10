// ────────────────────────────────────────────────────────────────────
// ProducerTestRunner.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Executes producer throughput benchmarks across all 5
//           dimensions: Serialization (Avro/JSON), Payload Size
//           (Small/Large), Record Type (Generic/Specific), Produce API
//           (Produce/ProduceAsync), and Commit Strategy (Single/
//           BatchConfigurable/Batch5K). Uses a unified produce loop.
// ────────────────────────────────────────────────────────────────────

using System.Diagnostics;
using Avro;
using Avro.Generic;
using Avro.Specific;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using ConfluentThroughputTestHarness.Config;
using ConfluentThroughputTestHarness.DataFactories;
using ConfluentThroughputTestHarness.Metrics;
using ConfluentThroughputTestHarness.Models;
using ConfluentThroughputTestHarness.Models.AvroSpecific;
using ConfluentThroughputTestHarness.Reporting;
using ConfluentThroughputTestHarness.Tests;

namespace ConfluentThroughputTestHarness.Runners;

public class ProducerTestRunner
{
    private readonly KafkaSettings _kafkaSettings;
    private readonly SchemaRegistrySettings _srSettings;
    private readonly TestSettings _testSettings;
    private readonly RecordSchema _smallSchema;
    private readonly RecordSchema _largeSchema;
    private readonly DeliveryLogger? _deliveryLogger;

    public ProducerTestRunner(
        KafkaSettings kafkaSettings,
        SchemaRegistrySettings srSettings,
        TestSettings testSettings,
        RecordSchema smallSchema,
        RecordSchema largeSchema,
        DeliveryLogger? deliveryLogger = null)
    {
        _kafkaSettings = kafkaSettings;
        _srSettings = srSettings;
        _testSettings = testSettings;
        _smallSchema = smallSchema;
        _largeSchema = largeSchema;
        _deliveryLogger = deliveryLogger;
    }

    /// <summary>
    /// Entry point for a single producer benchmark run.
    /// Routes to business-realistic (T3.x) or drag-race (T1.x) code paths
    /// based on the commit strategy, then selects the appropriate serializer
    /// setup based on format, payload size, and record type.
    /// </summary>
    public async Task<TestResult> RunAsync(TestDefinition test, int runNumber,
        Action<int, TimeSpan>? onProgress = null)
    {
        // T3.x business-realistic tests use a separate code path with
        // acks=all, idempotence, and Task.WhenAll concurrency windows.
        if (test.CommitStrategy == CommitStrategy.ConcurrencyWindow)
            return await RunBusinessRealisticAsync(test, runNumber, onProgress);

        int batchSize = test.CommitStrategy switch
        {
            CommitStrategy.Single => 1,
            CommitStrategy.BatchConfigurable => _testSettings.BatchCommitSize,
            CommitStrategy.Batch5K => 5_000,
            _ => throw new ArgumentOutOfRangeException(nameof(test.CommitStrategy))
        };

        return (test.Format, test.Size, test.RecordType) switch
        {
            (SerializationFormat.Avro, PayloadSize.Small, RecordType.GenericRecord) =>
                await RunAvroGenericAsync(test, runNumber, batchSize,
                    _smallSchema, new AvroSmallDataFactory(_smallSchema), onProgress),

            (SerializationFormat.Avro, PayloadSize.Large, RecordType.GenericRecord) =>
                await RunAvroGenericAsync(test, runNumber, batchSize,
                    _largeSchema, new AvroLargeDataFactory(_largeSchema), onProgress),

            (SerializationFormat.Avro, PayloadSize.Small, RecordType.SpecificRecord) =>
                await RunAvroSpecificAsync(test, runNumber, batchSize,
                    new AvroSmallSpecificDataFactory(), onProgress),

            (SerializationFormat.Avro, PayloadSize.Large, RecordType.SpecificRecord) =>
                await RunAvroSpecificAsync(test, runNumber, batchSize,
                    new AvroLargeSpecificDataFactory(), onProgress),

            (SerializationFormat.Json, PayloadSize.Small, _) =>
                await RunJsonTypedAsync(test, runNumber, batchSize,
                    new JsonSmallDataFactory(), onProgress),

            (SerializationFormat.Json, PayloadSize.Large, _) =>
                await RunJsonTypedAsync(test, runNumber, batchSize,
                    new JsonLargeDataFactory(), onProgress),

            _ => throw new ArgumentException(
                $"Unsupported test combination: {test.Format}/{test.Size}/{test.RecordType}")
        };
    }

    // ── Business-realistic routing (T3.x) ─────────────────────────────

    /// <summary>
    /// Routes T3.x business-realistic tests to the appropriate setup method.
    /// All T3.x tests use ProduceAsync + acks=all + idempotence + Task.WhenAll.
    /// Avro tests use SpecificRecord only (matching client's source-generated classes).
    /// </summary>
    private async Task<TestResult> RunBusinessRealisticAsync(TestDefinition test, int runNumber,
        Action<int, TimeSpan>? onProgress)
    {
        return (test.Format, test.Size) switch
        {
            (SerializationFormat.Avro, PayloadSize.Small) =>
                await RunBusinessAvroSpecificAsync(test, runNumber,
                    new AvroSmallSpecificDataFactory(), onProgress),

            (SerializationFormat.Avro, PayloadSize.Large) =>
                await RunBusinessAvroSpecificAsync(test, runNumber,
                    new AvroLargeSpecificDataFactory(), onProgress),

            (SerializationFormat.Json, PayloadSize.Small) =>
                await RunBusinessJsonTypedAsync(test, runNumber,
                    new JsonSmallDataFactory(), onProgress),

            (SerializationFormat.Json, PayloadSize.Large) =>
                await RunBusinessJsonTypedAsync(test, runNumber,
                    new JsonLargeDataFactory(), onProgress),

            _ => throw new ArgumentException(
                $"Unsupported T3.x combination: {test.Format}/{test.Size}")
        };
    }

    // ── Avro GenericRecord setup ────────────────────────────────────────

    private async Task<TestResult> RunAvroGenericAsync(
        TestDefinition test, int runNumber, int batchSize,
        RecordSchema schema, ITestDataFactory<GenericRecord> factory,
        Action<int, TimeSpan>? onProgress)
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var avroConfig = new AvroSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var keySerializer = new AvroSerializer<int>(schemaRegistry, avroConfig);
        var valueSerializer = new AvroSerializer<GenericRecord>(schemaRegistry, avroConfig);

        using var producer = BuildAvroProducer(producerConfig, test.ProduceApi, keySerializer, valueSerializer);

        return await RunProducerLoopAsync(test, runNumber, producer, record, factory, batchSize,
            EstimateAvroGenericBytes, onProgress);
    }

    // ── Avro SpecificRecord setup ───────────────────────────────────────

    private async Task<TestResult> RunAvroSpecificAsync<T>(
        TestDefinition test, int runNumber, int batchSize,
        ITestDataFactory<T> factory,
        Action<int, TimeSpan>? onProgress) where T : ISpecificRecord
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var avroConfig = new AvroSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var keySerializer = new AvroSerializer<int>(schemaRegistry, avroConfig);
        var valueSerializer = new AvroSerializer<T>(schemaRegistry, avroConfig);

        using var producer = BuildAvroProducer(producerConfig, test.ProduceApi, keySerializer, valueSerializer);

        return await RunProducerLoopAsync(test, runNumber, producer, record, factory, batchSize,
            EstimateAvroSpecificBytes, onProgress);
    }

    // ── JSON setup ──────────────────────────────────────────────────────

    private async Task<TestResult> RunJsonTypedAsync<T>(
        TestDefinition test, int runNumber, int batchSize,
        ITestDataFactory<T> factory,
        Action<int, TimeSpan>? onProgress) where T : class
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var jsonConfig = new JsonSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var jsonSerializer = new JsonSerializer<T>(schemaRegistry, jsonConfig);

        // ProduceAsync: set IAsyncSerializer directly; Produce: wrap with AsSyncOverAsync
        using var producer = test.ProduceApi == ProduceApi.ProduceAsync
            ? new ProducerBuilder<int, T>(producerConfig)
                .SetValueSerializer(jsonSerializer)
                .Build()
            : new ProducerBuilder<int, T>(producerConfig)
                .SetValueSerializer(jsonSerializer.AsSyncOverAsync())
                .Build();

        return await RunProducerLoopAsync(test, runNumber, producer, record, factory, batchSize,
            EstimateJsonBytes, onProgress);
    }

    // ── Business-realistic Avro SpecificRecord setup (T3.x) ────────────

    private async Task<TestResult> RunBusinessAvroSpecificAsync<T>(
        TestDefinition test, int runNumber,
        ITestDataFactory<T> factory,
        Action<int, TimeSpan>? onProgress) where T : ISpecificRecord
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildBusinessRealisticProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var avroConfig = new AvroSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var valueSerializer = new AvroSerializer<T>(schemaRegistry, avroConfig);

        // T3.x always uses ProduceAsync — set IAsyncSerializer directly
        using var producer = new ProducerBuilder<int, T>(producerConfig)
            .SetKeySerializer(new AvroSerializer<int>(schemaRegistry, avroConfig).AsSyncOverAsync())
            .SetValueSerializer(valueSerializer)
            .Build();

        return await RunConcurrencyWindowLoopAsync(test, runNumber, producer, record, factory,
            test.ConcurrencyWindow, EstimateAvroSpecificBytes, onProgress);
    }

    // ── Business-realistic JSON setup (T3.x) ─────────────────────────

    private async Task<TestResult> RunBusinessJsonTypedAsync<T>(
        TestDefinition test, int runNumber,
        ITestDataFactory<T> factory,
        Action<int, TimeSpan>? onProgress) where T : class
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildBusinessRealisticProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var jsonConfig = new JsonSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var jsonSerializer = new JsonSerializer<T>(schemaRegistry, jsonConfig);

        // T3.x always uses ProduceAsync — set IAsyncSerializer directly
        using var producer = new ProducerBuilder<int, T>(producerConfig)
            .SetValueSerializer(jsonSerializer)
            .Build();

        return await RunConcurrencyWindowLoopAsync(test, runNumber, producer, record, factory,
            test.ConcurrencyWindow, EstimateJsonBytes, onProgress);
    }

    // ── Concurrency window produce loop (T3.x) ───────────────────────

    /// <summary>
    /// Business-realistic produce loop that fires windowSize ProduceAsync calls
    /// concurrently, awaits all with Task.WhenAll, and checks each DeliveryResult
    /// for errors. Models real-world background services with guaranteed delivery
    /// (acks=all + idempotence).
    /// </summary>
    private async Task<TestResult> RunConcurrencyWindowLoopAsync<TValue>(
        TestDefinition test, int runNumber,
        IProducer<int, TValue> producer,
        TValue record,
        ITestDataFactory<TValue> factory,
        int windowSize,
        Func<TValue, int, long> estimateBytes,
        Action<int, TimeSpan>? onProgress)
    {
        using var monitor = new ResourceMonitor();
        var errors = 0;
        var messageCount = 0;
        var lastProgressReport = TimeSpan.Zero;

        var sw = Stopwatch.StartNew();

        while (ShouldContinueProducing(test, messageCount, sw))
        {
            // Determine batch size: don't exceed message count limit
            var remaining = test.MessageCount - messageCount;
            var batch = Math.Min(windowSize, remaining);
            if (batch <= 0) break;

            var tasks = new List<Task<DeliveryResult<int, TValue>>>(batch);

            for (var i = 0; i < batch; i++)
            {
                messageCount++;
                factory.SetMessageHeader(record, messageCount, DateTime.UtcNow.ToString("O"));
                var message = new Message<int, TValue> { Key = messageCount, Value = record };
                tasks.Add(producer.ProduceAsync(test.Topic, message));
            }

            var results = await Task.WhenAll(tasks);
            foreach (var dr in results)
            {
                if (dr.Status == PersistenceStatus.NotPersisted)
                {
                    Interlocked.Increment(ref errors);
                    _deliveryLogger?.LogError(test.Id, test.Name, runNumber,
                        dr.Key, dr.Partition.Value, dr.Offset.Value,
                        "NotPersisted", dr.Status.ToString());
                }
                else
                {
                    _deliveryLogger?.LogSuccess(test.Id, test.Name, runNumber,
                        dr.Key, dr.Partition.Value, dr.Offset.Value);
                }
            }

            if (onProgress != null && sw.Elapsed - lastProgressReport >= TimeSpan.FromSeconds(1))
            {
                onProgress(messageCount, sw.Elapsed);
                lastProgressReport = sw.Elapsed;
            }
        }

        producer.Flush(TimeSpan.FromSeconds(60));
        sw.Stop();

        return new TestResult
        {
            TestId = test.Id,
            TestName = test.Name,
            ProduceApi = test.ProduceApi.ToString(),
            CommitStrategy = test.CommitStrategy.ToString(),
            RecordType = test.RecordType.ToString(),
            ConcurrencyWindow = test.ConcurrencyWindow,
            RunNumber = runNumber,
            MessageCount = messageCount,
            TotalBytes = estimateBytes(record, messageCount),
            Elapsed = sw.Elapsed,
            PeakCpuPercent = monitor.PeakCpuPercent,
            PeakMemoryBytes = monitor.PeakMemoryBytes,
            DeliveryErrors = errors
        };
    }

    // ── Unified produce loop ────────────────────────────────────────────

    /// <summary>
    /// Unified produce loop that handles all 5 dimensions. The caller sets up the
    /// producer with the right serializers; this method drives the message loop,
    /// branching on ProduceApi and flushing per the commit strategy (via batchSize).
    /// </summary>
    private async Task<TestResult> RunProducerLoopAsync<TValue>(
        TestDefinition test, int runNumber,
        IProducer<int, TValue> producer,
        TValue record,
        ITestDataFactory<TValue> factory,
        int batchSize,
        Func<TValue, int, long> estimateBytes,
        Action<int, TimeSpan>? onProgress)
    {
        using var monitor = new ResourceMonitor();
        var errors = 0;
        var messageCount = 0;
        var lastProgressReport = TimeSpan.Zero;

        var sw = Stopwatch.StartNew();

        while (ShouldContinueProducing(test, messageCount, sw))
        {
            messageCount++;
            factory.SetMessageHeader(record, messageCount, DateTime.UtcNow.ToString("O"));

            var message = new Message<int, TValue> { Key = messageCount, Value = record };

            if (test.ProduceApi == ProduceApi.ProduceAsync)
            {
                var result = await producer.ProduceAsync(test.Topic, message);
                if (result.Status == PersistenceStatus.NotPersisted)
                {
                    Interlocked.Increment(ref errors);
                    _deliveryLogger?.LogError(test.Id, test.Name, runNumber,
                        result.Key, result.Partition.Value, result.Offset.Value,
                        "NotPersisted", result.Status.ToString());
                }
            }
            else
            {
                producer.Produce(test.Topic, message, dr =>
                {
                    if (dr.Error.IsError)
                    {
                        Interlocked.Increment(ref errors);
                        _deliveryLogger?.LogError(test.Id, test.Name, runNumber,
                            dr.Key, dr.Partition.Value, dr.Offset.Value,
                            dr.Error.Code.ToString(), dr.Error.Reason);
                    }
                });
            }

            // Flush per commit strategy: Single (batchSize=1) flushes every message,
            // BatchConfigurable flushes every settings.BatchCommitSize messages,
            // Batch5K flushes every 5,000 messages.
            if (messageCount % batchSize == 0)
                producer.Flush(TimeSpan.FromSeconds(30));

            if (onProgress != null && sw.Elapsed - lastProgressReport >= TimeSpan.FromSeconds(1))
            {
                onProgress(messageCount, sw.Elapsed);
                lastProgressReport = sw.Elapsed;
            }
        }

        producer.Flush(TimeSpan.FromSeconds(60));
        sw.Stop();

        return new TestResult
        {
            TestId = test.Id,
            TestName = test.Name,
            ProduceApi = test.ProduceApi.ToString(),
            CommitStrategy = test.CommitStrategy.ToString(),
            RecordType = test.RecordType.ToString(),
            ConcurrencyWindow = test.ConcurrencyWindow,
            RunNumber = runNumber,
            MessageCount = messageCount,
            TotalBytes = estimateBytes(record, messageCount),
            Elapsed = sw.Elapsed,
            PeakCpuPercent = monitor.PeakCpuPercent,
            PeakMemoryBytes = monitor.PeakMemoryBytes,
            DeliveryErrors = errors
        };
    }

    // ── Producer builder helpers ────────────────────────────────────────

    /// <summary>
    /// Builds an Avro producer with the correct serializer wiring for the produce API.
    /// Produce path: value serializer wrapped with AsSyncOverAsync().
    /// ProduceAsync path: async value serializer set directly (IAsyncSerializer).
    /// Key serializer always uses AsSyncOverAsync (int serialization is trivial).
    /// </summary>
    private static IProducer<int, TValue> BuildAvroProducer<TValue>(
        ProducerConfig config,
        ProduceApi produceApi,
        AvroSerializer<int> keySerializer,
        AvroSerializer<TValue> valueSerializer)
    {
        if (produceApi == ProduceApi.ProduceAsync)
        {
            return new ProducerBuilder<int, TValue>(config)
                .SetKeySerializer(keySerializer.AsSyncOverAsync())
                .SetValueSerializer(valueSerializer)
                .Build();
        }

        return new ProducerBuilder<int, TValue>(config)
            .SetKeySerializer(keySerializer.AsSyncOverAsync())
            .SetValueSerializer(valueSerializer.AsSyncOverAsync())
            .Build();
    }

    // ── Byte estimation helpers ─────────────────────────────────────────

    private static long EstimateAvroGenericBytes(GenericRecord record, int messageCount)
    {
        using var ms = new MemoryStream();
        var writer = new GenericDatumWriter<GenericRecord>(record.Schema);
        var encoder = new Avro.IO.BinaryEncoder(ms);
        writer.Write(record, encoder);
        encoder.Flush();
        var bytesPerMessage = ms.Length + 5;   // +5 = SR wire format header
        return bytesPerMessage * messageCount;
    }

    private static long EstimateAvroSpecificBytes<T>(T record, int messageCount) where T : ISpecificRecord
    {
        using var ms = new MemoryStream();
        var writer = new SpecificDatumWriter<T>(record.Schema);
        var encoder = new Avro.IO.BinaryEncoder(ms);
        writer.Write(record, encoder);
        encoder.Flush();
        var bytesPerMessage = ms.Length + 5;
        return bytesPerMessage * messageCount;
    }

    private static long EstimateJsonBytes<T>(T record, int messageCount)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(record);
        var bytesPerMessage = System.Text.Encoding.UTF8.GetByteCount(json) + 5;
        return (long)bytesPerMessage * messageCount;
    }

    // ── Shared helpers ──────────────────────────────────────────────────

    private static bool ShouldContinueProducing(TestDefinition test, int messageCount, Stopwatch sw)
    {
        return messageCount < test.MessageCount
            && (!test.Duration.HasValue || sw.Elapsed < test.Duration.Value);
    }

    private ProducerConfig BuildProducerConfig()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            SecurityProtocol = Enum.Parse<SecurityProtocol>(_kafkaSettings.SecurityProtocol, true),
            SaslMechanism = Enum.Parse<SaslMechanism>(_kafkaSettings.SaslMechanism, true),
            SaslUsername = _kafkaSettings.SaslUsername,
            SaslPassword = _kafkaSettings.SaslPassword,
            Acks = Enum.Parse<Acks>(_kafkaSettings.Acks, true),
            LingerMs = _kafkaSettings.LingerMs,
            BatchSize = _kafkaSettings.BatchSize,
            CompressionType = Enum.Parse<CompressionType>(_kafkaSettings.CompressionType, true),
            EnableIdempotence = false
        };
        // Suppress rdkafka informational logs (e.g., telemetry instance id changes)
        config.Set("log_level", "3"); // 3 = error
        return config;
    }

    /// <summary>
    /// Builds the producer configuration for T3.x business-realistic tests.
    /// Key differences from the drag-race config:
    /// - Acks=All: wait for all in-sync replicas to acknowledge (guaranteed delivery)
    /// - EnableIdempotence=true: exactly-once semantics, no duplicates on retry
    /// </summary>
    private ProducerConfig BuildBusinessRealisticProducerConfig()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            SecurityProtocol = Enum.Parse<SecurityProtocol>(_kafkaSettings.SecurityProtocol, true),
            SaslMechanism = Enum.Parse<SaslMechanism>(_kafkaSettings.SaslMechanism, true),
            SaslUsername = _kafkaSettings.SaslUsername,
            SaslPassword = _kafkaSettings.SaslPassword,
            Acks = Acks.All,
            EnableIdempotence = true,
            LingerMs = _kafkaSettings.LingerMs,
            BatchSize = _kafkaSettings.BatchSize,
            CompressionType = Enum.Parse<CompressionType>(_kafkaSettings.CompressionType, true),
        };
        config.Set("log_level", "3");
        return config;
    }

    private SchemaRegistryConfig BuildSchemaRegistryConfig() => new()
    {
        Url = _srSettings.Url,
        BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo,
        BasicAuthUserInfo = _srSettings.BasicAuthUserInfo
    };
}
