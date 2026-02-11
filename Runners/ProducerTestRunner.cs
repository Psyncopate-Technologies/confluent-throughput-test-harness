// ────────────────────────────────────────────────────────────────────
// ProducerTestRunner.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Executes producer throughput benchmarks across 3 scenarios:
//           Fire-and-Forget (T1.x), Request-Response (T2.x), and
//           Batch Processing (T3.x). All producers use acks=all +
//           enable.idempotence=true.
// ────────────────────────────────────────────────────────────────────

using System.Diagnostics;
using Avro;
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
    private readonly DeliveryLogger? _deliveryLogger;

    public ProducerTestRunner(
        KafkaSettings kafkaSettings,
        SchemaRegistrySettings srSettings,
        TestSettings testSettings,
        DeliveryLogger? deliveryLogger = null)
    {
        _kafkaSettings = kafkaSettings;
        _srSettings = srSettings;
        _testSettings = testSettings;
        _deliveryLogger = deliveryLogger;
    }

    /// <summary>
    /// Entry point for a single producer benchmark run.
    /// Routes to the appropriate scenario based on commit strategy.
    /// </summary>
    public async Task<TestResult> RunAsync(TestDefinition test, int runNumber,
        Action<int, TimeSpan>? onProgress = null)
    {
        return test.CommitStrategy switch
        {
            CommitStrategy.DeliveryHandler => await RunFireAndForgetAsync(test, runNumber, onProgress),
            CommitStrategy.RequestResponse => await RunRequestResponseAsync(test, runNumber, onProgress),
            CommitStrategy.ConcurrencyWindow => await RunBatchAsync(test, runNumber, onProgress),
            _ => throw new ArgumentOutOfRangeException(nameof(test.CommitStrategy))
        };
    }

    // ── Fire-and-Forget routing (T1.x) ──────────────────────────────

    private async Task<TestResult> RunFireAndForgetAsync(TestDefinition test, int runNumber,
        Action<int, TimeSpan>? onProgress)
    {
        return (test.Format, test.Size) switch
        {
            (SerializationFormat.Avro, PayloadSize.Small) =>
                await RunFireAndForgetAvroSpecificAsync(test, runNumber,
                    new AvroSmallSpecificDataFactory(), onProgress),

            (SerializationFormat.Avro, PayloadSize.Large) =>
                await RunFireAndForgetAvroSpecificAsync(test, runNumber,
                    new AvroLargeSpecificDataFactory(), onProgress),

            (SerializationFormat.Json, PayloadSize.Small) =>
                await RunFireAndForgetJsonTypedAsync(test, runNumber,
                    new JsonSmallDataFactory(), onProgress),

            (SerializationFormat.Json, PayloadSize.Large) =>
                await RunFireAndForgetJsonTypedAsync(test, runNumber,
                    new JsonLargeDataFactory(), onProgress),

            _ => throw new ArgumentException(
                $"Unsupported fire-and-forget combination: {test.Format}/{test.Size}")
        };
    }

    // ── Request-Response routing (T2.x) ─────────────────────────────

    private async Task<TestResult> RunRequestResponseAsync(TestDefinition test, int runNumber,
        Action<int, TimeSpan>? onProgress)
    {
        return (test.Format, test.Size) switch
        {
            (SerializationFormat.Avro, PayloadSize.Small) =>
                await RunRequestResponseAvroSpecificAsync(test, runNumber,
                    new AvroSmallSpecificDataFactory(), onProgress),

            (SerializationFormat.Avro, PayloadSize.Large) =>
                await RunRequestResponseAvroSpecificAsync(test, runNumber,
                    new AvroLargeSpecificDataFactory(), onProgress),

            (SerializationFormat.Json, PayloadSize.Small) =>
                await RunRequestResponseJsonTypedAsync(test, runNumber,
                    new JsonSmallDataFactory(), onProgress),

            (SerializationFormat.Json, PayloadSize.Large) =>
                await RunRequestResponseJsonTypedAsync(test, runNumber,
                    new JsonLargeDataFactory(), onProgress),

            _ => throw new ArgumentException(
                $"Unsupported request-response combination: {test.Format}/{test.Size}")
        };
    }

    // ── Batch Processing routing (T3.x) ─────────────────────────────

    private async Task<TestResult> RunBatchAsync(TestDefinition test, int runNumber,
        Action<int, TimeSpan>? onProgress)
    {
        return (test.Format, test.Size) switch
        {
            (SerializationFormat.Avro, PayloadSize.Small) =>
                await RunBatchAvroSpecificAsync(test, runNumber,
                    new AvroSmallSpecificDataFactory(), onProgress),

            (SerializationFormat.Avro, PayloadSize.Large) =>
                await RunBatchAvroSpecificAsync(test, runNumber,
                    new AvroLargeSpecificDataFactory(), onProgress),

            (SerializationFormat.Json, PayloadSize.Small) =>
                await RunBatchJsonTypedAsync(test, runNumber,
                    new JsonSmallDataFactory(), onProgress),

            (SerializationFormat.Json, PayloadSize.Large) =>
                await RunBatchJsonTypedAsync(test, runNumber,
                    new JsonLargeDataFactory(), onProgress),

            _ => throw new ArgumentException(
                $"Unsupported batch combination: {test.Format}/{test.Size}")
        };
    }

    // ── Fire-and-Forget Avro SpecificRecord setup ───────────────────

    private async Task<TestResult> RunFireAndForgetAvroSpecificAsync<T>(
        TestDefinition test, int runNumber,
        ITestDataFactory<T> factory,
        Action<int, TimeSpan>? onProgress) where T : ISpecificRecord
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var avroConfig = new AvroSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var keySerializer = new AvroSerializer<int>(schemaRegistry, avroConfig);
        var valueSerializer = new AvroSerializer<T>(schemaRegistry, avroConfig);

        using var producer = new ProducerBuilder<int, T>(producerConfig)
            .SetKeySerializer(keySerializer.AsSyncOverAsync())
            .SetValueSerializer(valueSerializer.AsSyncOverAsync())
            .Build();

        return await RunDeliveryHandlerLoopAsync(test, runNumber, producer, record, factory,
            EstimateAvroSpecificBytes, onProgress);
    }

    // ── Fire-and-Forget JSON setup ──────────────────────────────────

    private async Task<TestResult> RunFireAndForgetJsonTypedAsync<T>(
        TestDefinition test, int runNumber,
        ITestDataFactory<T> factory,
        Action<int, TimeSpan>? onProgress) where T : class
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var jsonConfig = new JsonSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var jsonSerializer = new JsonSerializer<T>(schemaRegistry, jsonConfig);

        using var producer = new ProducerBuilder<int, T>(producerConfig)
            .SetValueSerializer(jsonSerializer.AsSyncOverAsync())
            .Build();

        return await RunDeliveryHandlerLoopAsync(test, runNumber, producer, record, factory,
            EstimateJsonBytes, onProgress);
    }

    // ── Request-Response Avro SpecificRecord setup ──────────────────

    private async Task<TestResult> RunRequestResponseAvroSpecificAsync<T>(
        TestDefinition test, int runNumber,
        ITestDataFactory<T> factory,
        Action<int, TimeSpan>? onProgress) where T : ISpecificRecord
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var avroConfig = new AvroSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var valueSerializer = new AvroSerializer<T>(schemaRegistry, avroConfig);

        using var producer = new ProducerBuilder<int, T>(producerConfig)
            .SetKeySerializer(new AvroSerializer<int>(schemaRegistry, avroConfig).AsSyncOverAsync())
            .SetValueSerializer(valueSerializer)
            .Build();

        return await RunRequestResponseLoopAsync(test, runNumber, producer, record, factory,
            EstimateAvroSpecificBytes, onProgress);
    }

    // ── Request-Response JSON setup ─────────────────────────────────

    private async Task<TestResult> RunRequestResponseJsonTypedAsync<T>(
        TestDefinition test, int runNumber,
        ITestDataFactory<T> factory,
        Action<int, TimeSpan>? onProgress) where T : class
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var jsonConfig = new JsonSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var jsonSerializer = new JsonSerializer<T>(schemaRegistry, jsonConfig);

        using var producer = new ProducerBuilder<int, T>(producerConfig)
            .SetValueSerializer(jsonSerializer)
            .Build();

        return await RunRequestResponseLoopAsync(test, runNumber, producer, record, factory,
            EstimateJsonBytes, onProgress);
    }

    // ── Batch Avro SpecificRecord setup (T3.x) ─────────────────────

    private async Task<TestResult> RunBatchAvroSpecificAsync<T>(
        TestDefinition test, int runNumber,
        ITestDataFactory<T> factory,
        Action<int, TimeSpan>? onProgress) where T : ISpecificRecord
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var avroConfig = new AvroSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var valueSerializer = new AvroSerializer<T>(schemaRegistry, avroConfig);

        using var producer = new ProducerBuilder<int, T>(producerConfig)
            .SetKeySerializer(new AvroSerializer<int>(schemaRegistry, avroConfig).AsSyncOverAsync())
            .SetValueSerializer(valueSerializer)
            .Build();

        return await RunConcurrencyWindowLoopAsync(test, runNumber, producer, record, factory,
            test.ConcurrencyWindow, EstimateAvroSpecificBytes, onProgress);
    }

    // ── Batch JSON setup (T3.x) ────────────────────────────────────

    private async Task<TestResult> RunBatchJsonTypedAsync<T>(
        TestDefinition test, int runNumber,
        ITestDataFactory<T> factory,
        Action<int, TimeSpan>? onProgress) where T : class
    {
        var record = factory.CreateRecord();
        var producerConfig = BuildProducerConfig();
        using var schemaRegistry = new CachedSchemaRegistryClient(BuildSchemaRegistryConfig());
        var jsonConfig = new JsonSerializerConfig { AutoRegisterSchemas = false, UseLatestVersion = true };

        var jsonSerializer = new JsonSerializer<T>(schemaRegistry, jsonConfig);

        using var producer = new ProducerBuilder<int, T>(producerConfig)
            .SetValueSerializer(jsonSerializer)
            .Build();

        return await RunConcurrencyWindowLoopAsync(test, runNumber, producer, record, factory,
            test.ConcurrencyWindow, EstimateJsonBytes, onProgress);
    }

    // ── Delivery handler produce loop (T1.x fire-and-forget) ────────

    /// <summary>
    /// Fire-and-forget produce loop with a delivery handler callback.
    /// Calls producer.Produce(topic, message, deliveryHandler) for maximum throughput
    /// while still processing each delivery result individually via the callback.
    /// No explicit flush inside the loop — librdkafka batches via linger.ms/batch.size.
    /// Final Flush(60s) after the loop drains remaining in-flight messages.
    /// </summary>
    private async Task<TestResult> RunDeliveryHandlerLoopAsync<TValue>(
        TestDefinition test, int runNumber,
        IProducer<int, TValue> producer,
        TValue record,
        ITestDataFactory<TValue> factory,
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

            producer.Produce(test.Topic, message, dr =>
            {
                if (dr.Error.IsError)
                {
                    Interlocked.Increment(ref errors);
                    _deliveryLogger?.LogError(test.Id, test.Name, runNumber,
                        dr.Key, dr.Partition.Value, dr.Offset.Value,
                        dr.Error.Code.ToString(), dr.Error.Reason);
                }
                else
                {
                    _deliveryLogger?.LogSuccess(test.Id, test.Name, runNumber,
                        dr.Key, dr.Partition.Value, dr.Offset.Value);
                }
            });

            if (onProgress != null && sw.Elapsed - lastProgressReport >= TimeSpan.FromSeconds(1))
            {
                onProgress(messageCount, sw.Elapsed);
                lastProgressReport = sw.Elapsed;
            }
        }

        producer.Flush(TimeSpan.FromSeconds(60));
        sw.Stop();

        return await Task.FromResult(new TestResult
        {
            TestId = test.Id,
            TestName = test.Name,
            ProduceApi = test.ProduceApi.ToString(),
            CommitStrategy = test.CommitStrategy.ToString(),
            RecordType = test.RecordType.ToString(),
            ConcurrencyWindow = 0,
            RunNumber = runNumber,
            MessageCount = messageCount,
            TotalBytes = estimateBytes(record, messageCount),
            Elapsed = sw.Elapsed,
            PeakCpuPercent = monitor.PeakCpuPercent,
            PeakMemoryBytes = monitor.PeakMemoryBytes,
            DeliveryErrors = errors
        });
    }

    // ── Request-Response produce loop (T2.x) ────────────────────────

    /// <summary>
    /// Request-response produce loop that awaits each ProduceAsync call individually.
    /// Models synchronous request-response patterns where the caller needs confirmation
    /// before proceeding to the next message.
    /// </summary>
    private async Task<TestResult> RunRequestResponseLoopAsync<TValue>(
        TestDefinition test, int runNumber,
        IProducer<int, TValue> producer,
        TValue record,
        ITestDataFactory<TValue> factory,
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

            var result = await producer.ProduceAsync(test.Topic, message);
            if (result.Status == PersistenceStatus.NotPersisted)
            {
                Interlocked.Increment(ref errors);
                _deliveryLogger?.LogError(test.Id, test.Name, runNumber,
                    result.Key, result.Partition.Value, result.Offset.Value,
                    "NotPersisted", result.Status.ToString());
            }
            else
            {
                _deliveryLogger?.LogSuccess(test.Id, test.Name, runNumber,
                    result.Key, result.Partition.Value, result.Offset.Value);
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
            ConcurrencyWindow = 0,
            RunNumber = runNumber,
            MessageCount = messageCount,
            TotalBytes = estimateBytes(record, messageCount),
            Elapsed = sw.Elapsed,
            PeakCpuPercent = monitor.PeakCpuPercent,
            PeakMemoryBytes = monitor.PeakMemoryBytes,
            DeliveryErrors = errors
        };
    }

    // ── Concurrency window produce loop (T3.x batch) ────────────────

    /// <summary>
    /// Batch produce loop that fires windowSize ProduceAsync calls concurrently,
    /// awaits all with Task.WhenAll, and checks each DeliveryResult for errors.
    /// Uses a time-bound batch collection to prevent indefinite waiting when fewer
    /// events arrive than the window size.
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
            var remaining = test.MessageCount - messageCount;
            var batch = Math.Min(windowSize, remaining);
            if (batch <= 0) break;

            var tasks = new List<Task<DeliveryResult<int, TValue>>>(batch);

            var batchDeadline = DateTime.UtcNow.AddSeconds(_testSettings.BatchTimeoutSeconds);
            while (tasks.Count < batch
                && DateTime.UtcNow < batchDeadline
                && ShouldContinueProducing(test, messageCount, sw))
            {
                messageCount++;
                factory.SetMessageHeader(record, messageCount, DateTime.UtcNow.ToString("O"));
                var message = new Message<int, TValue> { Key = messageCount, Value = record };
                tasks.Add(producer.ProduceAsync(test.Topic, message));
            }

            if (tasks.Count == 0) break;

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

    // ── Byte estimation helpers ─────────────────────────────────────

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

    // ── Shared helpers ──────────────────────────────────────────────

    private static bool ShouldContinueProducing(TestDefinition test, int messageCount, Stopwatch sw)
    {
        return messageCount < test.MessageCount
            && (!test.Duration.HasValue || sw.Elapsed < test.Duration.Value);
    }

    /// <summary>
    /// Builds the producer configuration. All producer tests use acks=all +
    /// enable.idempotence=true for guaranteed delivery.
    /// </summary>
    private ProducerConfig BuildProducerConfig()
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
