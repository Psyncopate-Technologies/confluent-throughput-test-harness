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
    private readonly RecordSchema _smallSchema;
    private readonly RecordSchema _largeSchema;

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

    public async Task<TestResult> RunAsync(TestDefinition test, int runNumber)
    {
        return test.Format switch
        {
            SerializationFormat.Avro => await RunAvroAsync(test, runNumber),
            SerializationFormat.Json => await RunJsonAsync(test, runNumber),
            _ => throw new ArgumentException($"Unknown format: {test.Format}")
        };
    }

    private Task<TestResult> RunAvroAsync(TestDefinition test, int runNumber)
    {
        var schema = test.Size == PayloadSize.Small ? _smallSchema : _largeSchema;
        ITestDataFactory<GenericRecord> factory = test.Size == PayloadSize.Small
            ? new AvroSmallDataFactory(schema)
            : new AvroLargeDataFactory(schema);

        var record = factory.CreateRecord();

        var producerConfig = BuildProducerConfig();
        var schemaRegistryConfig = BuildSchemaRegistryConfig();

        using var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);
        var avroSerializerConfig = new AvroSerializerConfig
        {
            AutoRegisterSchemas = false,
            UseLatestVersion = true
        };
        using var producer = new ProducerBuilder<int, GenericRecord>(producerConfig)
            .SetKeySerializer(new AvroSerializer<int>(schemaRegistry, avroSerializerConfig).AsSyncOverAsync())
            .SetValueSerializer(new AvroSerializer<GenericRecord>(schemaRegistry, avroSerializerConfig).AsSyncOverAsync())
            .Build();

        using var monitor = new ResourceMonitor();
        var errors = 0;

        var sw = Stopwatch.StartNew();

        for (var i = 0; i < test.MessageCount; i++)
        {
            producer.Produce(test.Topic, new Message<int, GenericRecord> { Key = i + 1, Value = record },
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

    private Task<TestResult> RunJsonTypedAsync<T>(
        TestDefinition test, int runNumber,
        ISchemaRegistryClient schemaRegistry,
        ProducerConfig producerConfig,
        ITestDataFactory<T> factory) where T : class
    {
        var record = factory.CreateRecord();

        var jsonSerializerConfig = new JsonSerializerConfig
        {
            AutoRegisterSchemas = false,
            UseLatestVersion = true
        };
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

    private long EstimateAvroBytes(GenericRecord record, int messageCount)
    {
        using var ms = new MemoryStream();
        var writer = new Avro.Generic.GenericDatumWriter<GenericRecord>(record.Schema);
        var encoder = new Avro.IO.BinaryEncoder(ms);
        writer.Write(record, encoder);
        encoder.Flush();
        var payloadSize = ms.Length;
        var bytesPerMessage = payloadSize + 5;
        return bytesPerMessage * messageCount;
    }

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
        CompressionType = CompressionType.None,
        EnableIdempotence = false
    };

    private SchemaRegistryConfig BuildSchemaRegistryConfig() => new()
    {
        Url = _srSettings.Url,
        BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo,
        BasicAuthUserInfo = _srSettings.BasicAuthUserInfo
    };
}
