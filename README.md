# Confluent Kafka Throughput Test Harness

A .NET console application that benchmarks **Avro vs JSON** serialization throughput against a Confluent Cloud Kafka cluster. The harness measures producer and consumer performance across two real-world schema sizes, producing detailed metrics including messages/sec, MB/sec, average latency, peak CPU, and peak memory usage.

## Schemas

The test uses two Avro schemas derived from production workloads:

| Schema | Name | Fields | Description |
|--------|------|--------|-------------|
| **Small** | `TestAvroDataTypesMsg` | 58 | Mixed types: uuid, bool, string, int, long, float, double, decimal, timestamps, enums |
| **Large** | `freight_dbo_tblloads` | 104 | CDC freight loads table with varchar/char logicalTypes, decimals, timestamps, booleans |

Schema files are located in the `Schemas/` directory.

## Test Matrix

The harness runs 8 tests organized into producer and consumer groups. Each test produces or consumes **100,000 messages** (configurable) using a single identical record to isolate serialization overhead from object creation.

### Producer Tests (T1.x)

| Test | Format | Payload | Topic | Runs |
|------|--------|---------|-------|------|
| T1.1 | Avro | Small (58 fields) | `test-avro-small` | 3 |
| T1.2 | Avro | Large (104 fields) | `test-avro-large` | 3 |
| T1.3 | JSON | Small (58 fields) | `test-json-small` | 3 |
| T1.4 | JSON | Large (104 fields) | `test-json-large` | 3 |

### Consumer Tests (T2.x)

| Test | Format | Payload | Topic | Runs |
|------|--------|---------|-------|------|
| T2.1 | Avro | Small (58 fields) | `test-avro-small` | 5 |
| T2.2 | Avro | Large (104 fields) | `test-avro-large` | 5 |
| T2.3 | JSON | Small (58 fields) | `test-json-small` | 5 |
| T2.4 | JSON | Large (104 fields) | `test-json-large` | 5 |

Consumer tests read from the topics populated by the producer tests. Each consumer run uses a unique consumer group ID to ensure it reads from offset 0.

## Metrics Collected

For each test run, the harness captures:

- **Messages/sec** -- end-to-end throughput
- **MB/sec** -- data throughput based on serialized message size
- **Average latency (ms)** -- elapsed time / message count
- **Peak CPU %** -- sampled every 250ms during the run
- **Peak memory (MB)** -- working set high watermark
- **Delivery errors** -- count of failed produce/consume operations

Results are displayed in a formatted console table and exported to CSV.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (or later)
- A Confluent Cloud environment with:
  - A Kafka cluster
  - Schema Registry enabled
  - A service account with appropriate RBAC role bindings (see below)

## Confluent Cloud Setup

### 1. Service Account and RBAC

Create a service account and assign these Confluent RBAC role bindings:

```bash
# Create service account
confluent iam service-account create throughput-test-sa \
  --description "Service account for throughput test harness"

# Grant DeveloperRead and DeveloperWrite on topics prefixed "test-"
confluent iam rbac role-binding create \
  --principal User:<SERVICE_ACCOUNT_ID> \
  --role DeveloperRead \
  --environment <ENV_ID> --cloud-cluster <CLUSTER_ID> --kafka-cluster <CLUSTER_ID> \
  --resource "Topic:test-" --prefix

confluent iam rbac role-binding create \
  --principal User:<SERVICE_ACCOUNT_ID> \
  --role DeveloperWrite \
  --environment <ENV_ID> --cloud-cluster <CLUSTER_ID> --kafka-cluster <CLUSTER_ID> \
  --resource "Topic:test-" --prefix

# Grant DeveloperRead on consumer groups prefixed "throughput-test-"
confluent iam rbac role-binding create \
  --principal User:<SERVICE_ACCOUNT_ID> \
  --role DeveloperRead \
  --environment <ENV_ID> --cloud-cluster <CLUSTER_ID> --kafka-cluster <CLUSTER_ID> \
  --resource "Group:throughput-test-" --prefix

# Grant DeveloperRead and DeveloperWrite on Schema Registry subjects prefixed "test-"
confluent iam rbac role-binding create \
  --principal User:<SERVICE_ACCOUNT_ID> \
  --role DeveloperRead \
  --environment <ENV_ID> --schema-registry-cluster <SR_CLUSTER_ID> \
  --resource "Subject:test-" --prefix

confluent iam rbac role-binding create \
  --principal User:<SERVICE_ACCOUNT_ID> \
  --role DeveloperWrite \
  --environment <ENV_ID> --schema-registry-cluster <SR_CLUSTER_ID> \
  --resource "Subject:test-" --prefix
```

### 2. API Keys

Create API keys for both Kafka and Schema Registry:

```bash
# Kafka API key
confluent api-key create --service-account <SERVICE_ACCOUNT_ID> --resource <CLUSTER_ID>

# Schema Registry API key
confluent api-key create --service-account <SERVICE_ACCOUNT_ID> --resource <SR_CLUSTER_ID>
```

### 3. Topics

Create the four test topics. The harness expects 1 partition per topic (to avoid partition-level variability in benchmarks) with a short retention:

```bash
confluent kafka topic create test-avro-small  --partitions 1 --config "retention.ms=3600000"
confluent kafka topic create test-avro-large  --partitions 1 --config "retention.ms=3600000"
confluent kafka topic create test-json-small  --partitions 1 --config "retention.ms=3600000"
confluent kafka topic create test-json-large  --partitions 1 --config "retention.ms=3600000"
```

### 4. Schema Registration

Schemas must be registered ahead of time. The serializers are configured with `AutoRegisterSchemas = false` and `UseLatestVersion = true`, so they look up the latest registered schema by subject name at runtime.

```bash
# Register Avro schemas
confluent schema-registry schema create \
  --subject test-avro-small-value \
  --schema Schemas/TestAvroDataTypesMsg.g.avsc \
  --type avro

confluent schema-registry schema create \
  --subject test-avro-large-value \
  --schema Schemas/schema-cdc_freight_dbo_tblloads-value-v4.avsc \
  --type avro

# Register JSON schemas (auto-derived from POCO classes by the JSON serializer
# on first produce if AutoRegisterSchemas were true; since it's disabled,
# register them manually or enable auto-registration for the initial run)
```

> **Note on JSON schemas:** The `JsonSerializer<T>` with `UseLatestVersion = true` requires a schema to already exist for the subject. For the initial run of JSON tests (T1.3, T1.4), you may need to temporarily set `AutoRegisterSchemas = true` in the code, run the producer once, then revert.

## Configuration

The application uses the standard .NET configuration layering:

1. **`appsettings.json`** -- committed to the repo with placeholder values
2. **`appsettings.Development.json`** -- gitignored, contains real credentials
3. **Environment variables** -- highest priority, useful for CI/CD

Create `appsettings.Development.json` in the project root:

```json
{
  "Kafka": {
    "BootstrapServers": "pkc-xxxxx.region.provider.confluent.cloud:9092",
    "SaslUsername": "YOUR_KAFKA_API_KEY",
    "SaslPassword": "YOUR_KAFKA_API_SECRET"
  },
  "SchemaRegistry": {
    "Url": "https://psrc-xxxxx.region.provider.confluent.cloud",
    "BasicAuthUserInfo": "YOUR_SR_API_KEY:YOUR_SR_API_SECRET"
  }
}
```

### Configuration Reference

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| `Kafka` | `BootstrapServers` | -- | Confluent Cloud bootstrap server |
| `Kafka` | `SecurityProtocol` | `SaslSsl` | Security protocol |
| `Kafka` | `SaslMechanism` | `Plain` | SASL mechanism |
| `Kafka` | `SaslUsername` | -- | Kafka API key |
| `Kafka` | `SaslPassword` | -- | Kafka API secret |
| `Kafka` | `Acks` | `All` | Producer acknowledgment level |
| `Kafka` | `LingerMs` | `5` | Producer linger time in milliseconds |
| `Kafka` | `BatchSize` | `1000000` | Producer batch size in bytes |
| `SchemaRegistry` | `Url` | -- | Schema Registry endpoint URL |
| `SchemaRegistry` | `BasicAuthUserInfo` | -- | `API_KEY:API_SECRET` format |
| `Test` | `MessageCount` | `100000` | Messages per test run |
| `Test` | `ProducerRuns` | `3` | Number of runs per producer test |
| `Test` | `ConsumerRuns` | `5` | Number of runs per consumer test |
| `Test` | `AvroSmallTopic` | `test-avro-small` | Topic for small Avro payloads |
| `Test` | `AvroLargeTopic` | `test-avro-large` | Topic for large Avro payloads |
| `Test` | `JsonSmallTopic` | `test-json-small` | Topic for small JSON payloads |
| `Test` | `JsonLargeTopic` | `test-json-large` | Topic for large JSON payloads |

## Running the Tests

```bash
# Restore dependencies and build
dotnet restore
dotnet build

# Run all tests (producer tests first, then consumer tests)
dotnet run

# Run only producer tests
dotnet run -- --producer-only

# Run only consumer tests (topics must already contain messages)
dotnet run -- --consumer-only

# Run a specific test
dotnet run -- --test T1.2

# Show help
dotnet run -- --help
```

### Execution Flow

1. The application loads configuration from `appsettings.json`, `appsettings.Development.json`, and environment variables.
2. Both Avro schemas are parsed from the `Schemas/` directory. Custom logical types (`varchar`, `char`) are registered to handle the freight schema.
3. CLI arguments are parsed to determine which tests to run.
4. **Producer tests (T1.1--T1.4)** run first, each producing 100K messages across multiple runs. Messages are sent using `Produce()` (fire-and-forget with delivery handler) to avoid `Task` allocation overhead at high volume.
5. **Consumer tests (T2.1--T2.4)** run next, each consuming messages from the topics populated in step 4. Each run uses a unique consumer group (`throughput-test-{TestId}-run-{N}-{guid}`) to read from offset 0.
6. A formatted results table is printed to the console.
7. Results are exported to a timestamped CSV file in `bin/Debug/net9.0/results/`.

### Test Output

The console output includes:
- Per-run metrics for each test
- Averaged metrics per test
- A summary comparison table across all tests

CSV files are written to the `results/` directory under the build output with the format `throughput-results-YYYYMMDD-HHmmss.csv`.

## Project Structure

```
confluent-throughput-test-harness/
├── Program.cs                          # Main orchestrator and CLI
├── ConfluentThroughputTestHarness.csproj
├── appsettings.json                    # Template config (committed)
├── appsettings.Development.json        # Real credentials (gitignored)
├── Schemas/
│   ├── TestAvroDataTypesMsg.g.avsc     # Small schema (58 fields)
│   └── schema-cdc_freight_dbo_tblloads-value-v4.avsc  # Large schema (104 fields)
├── Config/
│   └── Settings.cs                     # KafkaSettings, SchemaRegistrySettings, TestSettings
├── LogicalTypes/
│   ├── VarcharLogicalType.cs           # Custom Avro logical type for varchar
│   └── CharLogicalType.cs             # Custom Avro logical type for char
├── Models/
│   ├── TestAvroDataTypesMsg.cs         # JSON POCO for small payload
│   └── FreightDboTblLoads.cs           # JSON POCO for large payload
├── DataFactories/
│   ├── ITestDataFactory.cs             # Factory interface
│   ├── AvroSmallDataFactory.cs         # GenericRecord builder (58 fields)
│   ├── AvroLargeDataFactory.cs         # GenericRecord builder (104 fields)
│   ├── JsonSmallDataFactory.cs         # POCO builder (58 fields)
│   └── JsonLargeDataFactory.cs         # POCO builder (104 fields)
├── Tests/
│   ├── TestDefinition.cs               # Test IDs, types, and configuration
│   ├── TestResult.cs                   # Per-run metrics
│   └── TestSuite.cs                    # Aggregation and averages
├── Metrics/
│   ├── ResourceMonitor.cs              # CPU/memory sampling (250ms interval)
│   └── ByteCountingDeserializer.cs     # Wraps deserializers for byte tracking
├── Runners/
│   ├── ProducerTestRunner.cs           # Producer benchmark (Avro + JSON)
│   └── ConsumerTestRunner.cs           # Consumer benchmark (Avro + JSON)
└── Reporting/
    ├── ConsoleReporter.cs              # Spectre.Console formatted tables
    └── CsvReporter.cs                  # CSV export
```

## Design Decisions

- **GenericRecord for Avro**: The large freight schema uses custom logical types (`varchar`, `char`) that are not supported by Avro code generation. `GenericRecord` with `Schema.Parse()` and custom logical type registration handles this cleanly.
- **POCO classes for JSON**: The `Confluent.SchemaRegistry.Serdes.Json` serializer works with plain C# classes annotated with `System.Text.Json` attributes.
- **`Produce()` over `ProduceAsync()`**: The synchronous fire-and-forget `Produce()` with a delivery callback avoids `Task` allocation GC pressure at high message volumes, giving more accurate throughput numbers.
- **Pre-registered schemas**: Serializers are configured with `AutoRegisterSchemas = false` and `UseLatestVersion = true` to download schemas by subject, ensuring the test environment is deterministic and repeatable.
- **Single record per test**: One record is created and sent 100K times to isolate serialization and network throughput from object construction overhead.
- **Unique consumer group per run**: Each consumer run creates a group ID like `throughput-test-T2.1-run-1-<guid>` so every run reads the full topic from the beginning.
