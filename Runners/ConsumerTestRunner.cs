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
        var consumerConfig = BuildConsumerConfig(test.Id, runNumber);
        var schemaRegistryConfig = BuildSchemaRegistryConfig();

        using var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);
        var avroDeserializer = new AvroDeserializer<GenericRecord>(schemaRegistry);
        var byteCounter = new ByteCountingAsyncDeserializer<GenericRecord>(avroDeserializer);

        using var consumer = new ConsumerBuilder<string, GenericRecord>(consumerConfig)
            .SetValueDeserializer(byteCounter.AsSyncOverAsync())
            .SetErrorHandler((_, e) => Console.Error.WriteLine($"Consumer error: {e.Reason}"))
            .Build();

        return Task.FromResult(ConsumeMessages(consumer, test, runNumber, () => byteCounter.TotalBytes));
    }

    private Task<TestResult> RunJsonAsync(TestDefinition test, int runNumber)
    {
        if (test.Size == PayloadSize.Small)
            return RunJsonTypedAsync<TestAvroDataTypesMsg>(test, runNumber);
        else
            return RunJsonTypedAsync<FreightDboTblLoads>(test, runNumber);
    }

    private Task<TestResult> RunJsonTypedAsync<T>(TestDefinition test, int runNumber) where T : class
    {
        var consumerConfig = BuildConsumerConfig(test.Id, runNumber);
        var schemaRegistryConfig = BuildSchemaRegistryConfig();

        using var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);
        var jsonDeserializer = new JsonDeserializer<T>();
        var byteCounter = new ByteCountingAsyncDeserializer<T>(jsonDeserializer);

        using var consumer = new ConsumerBuilder<string, T>(consumerConfig)
            .SetValueDeserializer(byteCounter.AsSyncOverAsync())
            .SetErrorHandler((_, e) => Console.Error.WriteLine($"Consumer error: {e.Reason}"))
            .Build();

        return Task.FromResult(ConsumeMessages(consumer, test, runNumber, () => byteCounter.TotalBytes));
    }

    private TestResult ConsumeMessages<TValue>(
        IConsumer<string, TValue> consumer,
        TestDefinition test,
        int runNumber,
        Func<long> getBytesConsumed)
    {
        consumer.Subscribe(test.Topic);

        using var monitor = new ResourceMonitor();
        var messagesConsumed = 0;
        var errors = 0;
        var timeout = TimeSpan.FromSeconds(5);

        var sw = Stopwatch.StartNew();

        while (messagesConsumed < test.MessageCount)
        {
            try
            {
                var result = consumer.Consume(timeout);
                if (result == null)
                {
                    // No more messages within timeout - topic may not have enough messages
                    Console.WriteLine($"  [WARNING] Timeout waiting for messages at {messagesConsumed}/{test.MessageCount}");
                    break;
                }

                if (result.IsPartitionEOF)
                {
                    // Reached end of partition, but may have more partitions
                    continue;
                }

                messagesConsumed++;
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
        consumer.Close();

        return new TestResult
        {
            TestId = test.Id,
            TestName = test.Name,
            RunNumber = runNumber,
            MessageCount = messagesConsumed,
            TotalBytes = getBytesConsumed(),
            Elapsed = sw.Elapsed,
            PeakCpuPercent = monitor.PeakCpuPercent,
            PeakMemoryBytes = monitor.PeakMemoryBytes,
            DeliveryErrors = errors
        };
    }

    private ConsumerConfig BuildConsumerConfig(string testId, int runNumber) => new()
    {
        BootstrapServers = _kafkaSettings.BootstrapServers,
        SecurityProtocol = Enum.Parse<SecurityProtocol>(_kafkaSettings.SecurityProtocol, true),
        SaslMechanism = Enum.Parse<SaslMechanism>(_kafkaSettings.SaslMechanism, true),
        SaslUsername = _kafkaSettings.SaslUsername,
        SaslPassword = _kafkaSettings.SaslPassword,
        GroupId = $"throughput-test-{testId}-run-{runNumber}-{Guid.NewGuid():N}",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = true,
        FetchMinBytes = 1,
        FetchMaxBytes = 52_428_800, // 50MB
        MaxPartitionFetchBytes = 10_485_760, // 10MB
        EnablePartitionEof = true
    };

    private SchemaRegistryConfig BuildSchemaRegistryConfig() => new()
    {
        Url = _srSettings.Url,
        BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo,
        BasicAuthUserInfo = _srSettings.BasicAuthUserInfo
    };
}
