// ────────────────────────────────────────────────────────────────────
// ProducerTestRunner.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Executes producer throughput benchmarks for both Avro and
//           JSON serialization formats. Produces messages using the
//           fire-and-forget Produce() pattern with delivery handlers
//           and collects timing, byte count, and resource metrics.
// ────────────────────────────────────────────────────────────────────

using System.Diagnostics;
using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using ConfluentThroughputTestHarness.Config;
using ConfluentThroughputTestHarness.DataFactories;
using ConfluentThroughputTestHarness.Metrics;
using ConfluentThroughputTestHarness.Models;
using ConfluentThroughputTestHarness.Tests;

namespace ConfluentThroughputTestHarness.Runners;

public class ProducerTestRunner
{
    private readonly KafkaSettings _kafkaSettings;
    private readonly SchemaRegistrySettings _srSettings;
    private readonly RecordSchema _smallSchema;   // Parsed Avro schema for the 25-field freight subset
    private readonly RecordSchema _largeSchema;    // Parsed Avro schema for the full 104-field freight table

    public ProducerTestRunner(
        KafkaSettings kafkaSettings,
        SchemaRegistrySettings srSettings,
        RecordSchema smallSchema,
        RecordSchema largeSchema)
    {
        _kafkaSettings = kafkaSettings;
        _srSettings = srSettings;
        _smallSchema = smallSchema;
        _largeSchema = largeSchema;
    }

    /// <summary>
    /// Entry point for a single producer benchmark run.
    /// Routes to the Avro or JSON code path based on the test definition's format.
    /// </summary>
    public async Task<TestResult> RunAsync(TestDefinition test, int runNumber)
    {
        return test.Format switch
        {
            SerializationFormat.Avro => await RunAvroAsync(test, runNumber),
            SerializationFormat.Json => await RunJsonAsync(test, runNumber),
            _ => throw new ArgumentException($"Unknown format: {test.Format}")
        };
    }

    /// <summary>
    /// Produces Avro-serialized messages to the test topic.
    ///
    /// Key design points:
    /// - Uses GenericRecord (not generated classes) because the freight schema has
    ///   custom logical types (varchar, char) that are incompatible with AvroGen.
    /// - Keys are sequential integers (1, 2, 3, ...) serialized with AvroSerializer&lt;int&gt;.
    /// - The AvroSerializer is configured with AutoRegisterSchemas=false and
    ///   UseLatestVersion=true, requiring schemas to be pre-registered in SR.
    /// - Uses Produce() (fire-and-forget) instead of ProduceAsync() to avoid
    ///   allocating a Task per message, which causes GC pressure at high volume.
    /// - A delivery handler callback tracks errors via Interlocked.Increment.
    /// - Flush is called every 50K messages and at the end to drain the internal
    ///   librdkafka buffer before stopping the timer.
    /// </summary>
    private Task<TestResult> RunAvroAsync(TestDefinition test, int runNumber)
    {
        // Select the schema and data factory based on payload size (small=25 fields, large=104 fields)
        var schema = test.Size == PayloadSize.Small ? _smallSchema : _largeSchema;
        ITestDataFactory<GenericRecord> factory = test.Size == PayloadSize.Small
            ? new AvroSmallDataFactory(schema)
            : new AvroLargeDataFactory(schema);

        // Create a single record that will be sent for all messages in this run.
        // This isolates serialization/network throughput from object construction overhead.
        var record = factory.CreateRecord();

        var producerConfig = BuildProducerConfig();
        var schemaRegistryConfig = BuildSchemaRegistryConfig();

        using var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);

        // Configure Avro serializers to look up pre-registered schemas by subject name.
        // AutoRegisterSchemas=false prevents accidental schema evolution during benchmarks.
        var avroSerializerConfig = new AvroSerializerConfig
        {
            AutoRegisterSchemas = false,
            UseLatestVersion = true
        };

        // Build the producer with Schema Registry-aware key and value serializers.
        // .AsSyncOverAsync() wraps the async SR serializer for use with the synchronous
        // Produce() method. This is required because Schema Registry serializers only
        // implement IAsyncSerializer, but Produce() needs ISerializer.
        using var producer = new ProducerBuilder<int, GenericRecord>(producerConfig)
            .SetKeySerializer(new AvroSerializer<int>(schemaRegistry, avroSerializerConfig).AsSyncOverAsync())
            .SetValueSerializer(new AvroSerializer<GenericRecord>(schemaRegistry, avroSerializerConfig).AsSyncOverAsync())
            .Build();

        // Start background CPU/memory sampling at 250ms intervals
        using var monitor = new ResourceMonitor();
        var errors = 0;

        var sw = Stopwatch.StartNew();

        for (var i = 0; i < test.MessageCount; i++)
        {
            // Fire-and-forget produce: the delivery handler runs asynchronously
            // on librdkafka's background thread when the broker acknowledges.
            producer.Produce(test.Topic, new Message<int, GenericRecord> { Key = i + 1, Value = record },
                dr =>
                {
                    if (dr.Error.IsError)
                        Interlocked.Increment(ref errors);
                });

            // Periodic flush to prevent the internal buffer from growing unbounded.
            // Without this, very fast producers can exhaust memory before messages
            // are transmitted to the broker.
            if (i > 0 && i % 50_000 == 0)
                producer.Flush(TimeSpan.FromSeconds(30));
        }

        // Final flush: wait for all in-flight messages to be acknowledged before
        // stopping the timer. This ensures elapsed time reflects true end-to-end latency.
        producer.Flush(TimeSpan.FromSeconds(60));
        sw.Stop();

        // Estimate total bytes by Avro-encoding a single record and multiplying.
        // The +5 accounts for the Schema Registry wire format header (1 magic byte + 4 schema ID bytes).
        var totalBytes = EstimateAvroBytes(record, test.MessageCount);

        return Task.FromResult(new TestResult
        {
            TestId = test.Id,
            TestName = test.Name,
            RunNumber = runNumber,
            MessageCount = test.MessageCount,
            TotalBytes = totalBytes,
            Elapsed = sw.Elapsed,
            PeakCpuPercent = monitor.PeakCpuPercent,
            PeakMemoryBytes = monitor.PeakMemoryBytes,
            DeliveryErrors = errors
        });
    }

    /// <summary>
    /// Produces JSON-serialized messages to the test topic.
    /// Routes to the generic RunJsonTypedAsync&lt;T&gt; with the appropriate POCO type
    /// based on payload size (FreightDboTblLoadsSmall or FreightDboTblLoads).
    /// </summary>
    private Task<TestResult> RunJsonAsync(TestDefinition test, int runNumber)
    {
        var producerConfig = BuildProducerConfig();
        var schemaRegistryConfig = BuildSchemaRegistryConfig();

        using var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);

        if (test.Size == PayloadSize.Small)
            return RunJsonTypedAsync<FreightDboTblLoadsSmall>(
                test, runNumber, schemaRegistry, producerConfig,
                new JsonSmallDataFactory());
        else
            return RunJsonTypedAsync<FreightDboTblLoads>(
                test, runNumber, schemaRegistry, producerConfig,
                new JsonLargeDataFactory());
    }

    /// <summary>
    /// Generic JSON producer benchmark. The type parameter T is the POCO class
    /// matching the JSON schema (FreightDboTblLoadsSmall or FreightDboTblLoads).
    ///
    /// Key differences from the Avro path:
    /// - Uses JsonSerializer&lt;T&gt; for values (requires T : class).
    /// - Keys use the default Serializers.Int32 (not JsonSerializer&lt;int&gt;) because
    ///   the Confluent JsonSerializer has a "where T : class" constraint that prevents
    ///   using it with value types like int.
    /// - Byte count is estimated by serializing one record to JSON and multiplying.
    /// </summary>
    private Task<TestResult> RunJsonTypedAsync<T>(
        TestDefinition test, int runNumber,
        ISchemaRegistryClient schemaRegistry,
        ProducerConfig producerConfig,
        ITestDataFactory<T> factory) where T : class
    {
        var record = factory.CreateRecord();

        // JSON serializer also uses pre-registered schemas (AutoRegisterSchemas=false)
        var jsonSerializerConfig = new JsonSerializerConfig
        {
            AutoRegisterSchemas = false,
            UseLatestVersion = true
        };

        // Note: no SetKeySerializer call -- the default Serializers.Int32 is used for int keys.
        // The JSON value serializer validates against the JSON Schema registered in SR.
        using var producer = new ProducerBuilder<int, T>(producerConfig)
            .SetValueSerializer(new JsonSerializer<T>(schemaRegistry, jsonSerializerConfig).AsSyncOverAsync())
            .Build();

        using var monitor = new ResourceMonitor();
        var errors = 0;

        var sw = Stopwatch.StartNew();

        for (var i = 0; i < test.MessageCount; i++)
        {
            producer.Produce(test.Topic, new Message<int, T> { Key = i + 1, Value = record },
                dr =>
                {
                    if (dr.Error.IsError)
                        Interlocked.Increment(ref errors);
                });

            if (i > 0 && i % 50_000 == 0)
                producer.Flush(TimeSpan.FromSeconds(30));
        }

        producer.Flush(TimeSpan.FromSeconds(60));
        sw.Stop();

        // Estimate bytes: serialize the record to JSON, measure its UTF-8 byte size,
        // then add 5 bytes for the Schema Registry wire format header.
        var json = System.Text.Json.JsonSerializer.Serialize(record);
        var bytesPerMessage = System.Text.Encoding.UTF8.GetByteCount(json) + 5;
        var totalBytes = (long)bytesPerMessage * test.MessageCount;

        return Task.FromResult(new TestResult
        {
            TestId = test.Id,
            TestName = test.Name,
            RunNumber = runNumber,
            MessageCount = test.MessageCount,
            TotalBytes = totalBytes,
            Elapsed = sw.Elapsed,
            PeakCpuPercent = monitor.PeakCpuPercent,
            PeakMemoryBytes = monitor.PeakMemoryBytes,
            DeliveryErrors = errors
        });
    }

    /// <summary>
    /// Estimates total Avro payload bytes by encoding a single record with the
    /// Avro BinaryEncoder and multiplying by message count. Adds 5 bytes per
    /// message for the Confluent Schema Registry wire format header
    /// (1 magic byte + 4-byte schema ID).
    /// </summary>
    private long EstimateAvroBytes(GenericRecord record, int messageCount)
    {
        using var ms = new MemoryStream();
        var writer = new Avro.Generic.GenericDatumWriter<GenericRecord>(record.Schema);
        var encoder = new Avro.IO.BinaryEncoder(ms);
        writer.Write(record, encoder);
        encoder.Flush();
        var payloadSize = ms.Length;
        var bytesPerMessage = payloadSize + 5;   // +5 = SR wire format header
        return bytesPerMessage * messageCount;
    }

    /// <summary>
    /// Builds the librdkafka producer configuration from application settings.
    /// Settings are optimized for throughput benchmarking:
    /// - Acks=Leader (1): only wait for the partition leader to acknowledge
    /// - LingerMs=100: batch messages for up to 100ms to maximize batch size
    /// - BatchSize=1MB: large batches reduce per-message overhead
    /// - LZ4 compression: reduces network I/O with minimal CPU cost
    /// - EnableIdempotence=false: not needed for benchmarks, avoids overhead
    /// </summary>
    private ProducerConfig BuildProducerConfig() => new()
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
