namespace ConfluentThroughputTestHarness.Config;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string SecurityProtocol { get; set; } = "SaslSsl";
    public string SaslMechanism { get; set; } = "Plain";
    public string SaslUsername { get; set; } = string.Empty;
    public string SaslPassword { get; set; } = string.Empty;
    public string Acks { get; set; } = "All";
    public int LingerMs { get; set; } = 5;
    public int BatchSize { get; set; } = 1000000;
}

public class SchemaRegistrySettings
{
    public string Url { get; set; } = string.Empty;
    public string BasicAuthUserInfo { get; set; } = string.Empty;
}

public class TestSettings
{
    public int MessageCount { get; set; } = 100_000;
    public int ProducerRuns { get; set; } = 3;
    public int ConsumerRuns { get; set; } = 5;
    public string TopicPrefix { get; set; } = "throughput-test";
    public string AvroSmallTopic { get; set; } = "throughput-test-avro-small";
    public string AvroLargeTopic { get; set; } = "throughput-test-avro-large";
    public string JsonSmallTopic { get; set; } = "throughput-test-json-small";
    public string JsonLargeTopic { get; set; } = "throughput-test-json-large";
}
