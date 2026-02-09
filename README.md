# Confluent Kafka Throughput Test Harness

A .NET console application that benchmarks **Avro vs JSON** serialization throughput against a Confluent Cloud Kafka cluster. The harness measures producer and consumer performance across two real-world schema sizes, producing detailed metrics including messages/sec, MB/sec, average latency, peak CPU, and peak memory usage.

## Schemas

Both value schemas are derived from a production freight CDC (`tblloads`) table. The small schema is a 27-field subset of the full 106-field large schema, covering a representative mix of data types: integers, strings (varchar/char), booleans, timestamps, and decimals. All message keys are integers.

Each schema includes two test header fields (`__test_seq` and `__test_ts`) embedded in the value payload. These are stamped with a unique sequence number and ISO timestamp before every `Produce()` call, ensuring each message has unique content and proving the serializer performs real serialization work on every message.

Each schema has both an Avro (`.avsc`) and a JSON Schema (`.json`) variant in the `Schemas/` directory, named to match their Schema Registry subject:

### Value Schemas

| File | SR Subject | Topic | Fields |
|------|------------|-------|--------|
| `test-avro-small-value.avsc` | `test-avro-small-value` | `test-avro-small` | 27 |
| `test-avro-large-value.avsc` | `test-avro-large-value` | `test-avro-large` | 106 |
| `test-json-small-value.json` | `test-json-small-value` | `test-json-small` | 27 |
| `test-json-large-value.json` | `test-json-large-value` | `test-json-large` | 106 |

### Key Schemas

| File | SR Subjects | Type |
|------|-------------|------|
| `test-avro-key.avsc` | `test-avro-small-key`, `test-avro-large-key` | Avro `int` |
| `test-json-key.json` | `test-json-small-key`, `test-json-large-key` | JSON `integer` |

## Test Matrix

The harness runs 8 tests organized into producer and consumer groups. It supports two execution modes:

- **Count-based (default)**: Each test produces or consumes a fixed number of messages (default 100,000).
- **Duration-based**: Each test runs for a specified number of minutes (e.g., `--duration 10`), producing or consuming as many messages as possible within that time window.

Each message gets a unique `__test_seq` (sequence number) and `__test_ts` (ISO timestamp) stamped into the value payload before every `Produce()` call, guaranteeing that the serializer performs real work on every message. Message keys are sequential integers (1, 2, 3, ...).

### Producer Tests (T1.x)

| Test | Format | Payload | Topic | Runs |
|------|--------|---------|-------|------|
| T1.1 | Avro | Small (27 fields) | `test-avro-small` | 3 |
| T1.2 | Avro | Large (106 fields) | `test-avro-large` | 3 |
| T1.3 | JSON | Small (27 fields) | `test-json-small` | 3 |
| T1.4 | JSON | Large (106 fields) | `test-json-large` | 3 |

### Consumer Tests (T2.x)

| Test | Format | Payload | Topic | Runs |
|------|--------|---------|-------|------|
| T2.1 | Avro | Small (27 fields) | `test-avro-small` | 5 |
| T2.2 | Avro | Large (106 fields) | `test-avro-large` | 5 |
| T2.3 | JSON | Small (27 fields) | `test-json-small` | 5 |
| T2.4 | JSON | Large (106 fields) | `test-json-large` | 5 |

Consumer tests read from the topics populated by the producer tests. Each consumer run uses a unique consumer group ID to ensure it reads from offset 0.

## Metrics Collected

For each test run, the harness captures:

- **Messages/sec** -- end-to-end throughput
- **MB/sec** -- data throughput based on serialized message size
- **Average latency (ms)** -- elapsed time / message count
- **Peak CPU %** -- sampled every 250ms during the run
- **Peak memory (MB)** -- working set high watermark
- **Delivery errors** -- count of failed produce/consume operations

In duration mode, time-series throughput samples are collected every second, capturing cumulative message counts over time. These samples power the interactive line charts in the HTML report.

Results are displayed in a formatted console table and exported to both CSV and an interactive HTML report.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (or later)
- A Confluent Cloud environment with:
  - A Kafka cluster
  - Schema Registry enabled
  - A service account with appropriate RBAC role bindings (see below)

## NuGet Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Confluent.Kafka` | 2.13.0 | Kafka producer and consumer client |
| `Confluent.SchemaRegistry` | 2.13.0 | Schema Registry client |
| `Confluent.SchemaRegistry.Serdes.Avro` | 2.13.0 | Avro serializer/deserializer with Schema Registry integration |
| `Confluent.SchemaRegistry.Serdes.Json` | 2.13.0 | JSON Schema serializer/deserializer with Schema Registry integration |
| `Microsoft.Extensions.Configuration.Json` | 10.0.2 | JSON configuration file provider |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | 10.0.2 | Environment variable configuration provider |
| `Microsoft.Extensions.Configuration.Binder` | 10.0.2 | Configuration binding to POCO settings classes |
| `Spectre.Console` | 0.54.0 | Rich console output with formatted tables and spinners |

All packages are restored automatically via `dotnet restore`.

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

All schemas must be pre-registered. The serializers are configured with `AutoRegisterSchemas = false` and `UseLatestVersion = true`.

```bash
# Value schemas
confluent schema-registry schema create \
  --subject test-avro-small-value \
  --schema Schemas/test-avro-small-value.avsc --type avro

confluent schema-registry schema create \
  --subject test-avro-large-value \
  --schema Schemas/test-avro-large-value.avsc --type avro

confluent schema-registry schema create \
  --subject test-json-small-value \
  --schema Schemas/test-json-small-value.json --type json

confluent schema-registry schema create \
  --subject test-json-large-value \
  --schema Schemas/test-json-large-value.json --type json

# Key schemas
confluent schema-registry schema create \
  --subject test-avro-small-key \
  --schema Schemas/test-avro-key.avsc --type avro

confluent schema-registry schema create \
  --subject test-avro-large-key \
  --schema Schemas/test-avro-key.avsc --type avro

confluent schema-registry schema create \
  --subject test-json-small-key \
  --schema Schemas/test-json-key.json --type json

confluent schema-registry schema create \
  --subject test-json-large-key \
  --schema Schemas/test-json-key.json --type json
```

This registers 8 subjects total (4 value + 4 key).

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
| `Kafka` | `Acks` | `Leader` | Producer acknowledgment level (`All` or `Leader`) |
| `Kafka` | `LingerMs` | `100` | Producer linger time in milliseconds |
| `Kafka` | `BatchSize` | `1000000` | Producer batch size in bytes |
| `Kafka` | `CompressionType` | `Lz4` | Producer compression (`None`, `Gzip`, `Snappy`, `Lz4`, `Zstd`) |
| `SchemaRegistry` | `Url` | -- | Schema Registry endpoint URL |
| `SchemaRegistry` | `BasicAuthUserInfo` | -- | `API_KEY:API_SECRET` format |
| `Test` | `MessageCount` | `100000` | Messages per test run (count mode) |
| `Test` | `DurationMinutes` | `null` | Minutes per test run (duration mode, overrides MessageCount) |
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
```

### CLI Flags

| Flag | Description |
|------|-------------|
| `--producer-only` | Run only producer tests (T1.1--T1.4) |
| `--consumer-only` | Run only consumer tests (T2.1--T2.4) |
| `--test <ID>` | Run a single test by ID (e.g., `T1.1`, `T2.3`) |
| `--duration <N>` | Run each test for N minutes instead of a fixed message count |
| `--help` | Show usage information |

### Execution Scenarios

```bash
# ── Count Mode (default: 100K messages per run) ─────────────────────

# Run all 8 tests (T1.1-T1.4 producers, then T2.1-T2.4 consumers)
dotnet run

# Run only the 4 producer tests
dotnet run -- --producer-only

# Run only the 4 consumer tests (topics must already contain messages)
dotnet run -- --consumer-only

# Run a single producer test
dotnet run -- --test T1.1

# Run a single consumer test
dotnet run -- --test T2.3

# ── Duration Mode (produce/consume for N minutes per run) ───────────

# Run all 8 tests, 10 minutes per run
dotnet run -- --duration 10

# Run only producer tests for 15 minutes each
dotnet run -- --producer-only --duration 15

# Run only consumer tests for 10 minutes each
dotnet run -- --consumer-only --duration 10

# Run a single producer test for 20 minutes
dotnet run -- --test T1.2 --duration 20

# Run a single consumer test for 10 minutes
dotnet run -- --test T2.4 --duration 10

# ── Help ────────────────────────────────────────────────────────────

# Show usage and test IDs
dotnet run -- --help
```

### Execution Flow

1. The application loads configuration from `appsettings.json`, `appsettings.Development.json`, and environment variables.
2. Both Avro schemas are parsed from the `Schemas/` directory. Custom logical types (`varchar`, `char`) are registered to handle the freight schema.
3. CLI arguments are parsed to determine which tests to run and whether to use count-based or duration-based mode (`--duration N`).
4. **Producer tests (T1.1--T1.4)** run first. In count mode, each run produces a fixed number of messages. In duration mode, each run produces continuously for the specified time. Before every `Produce()` call, the record's `__test_seq` and `__test_ts` fields are updated with a unique sequence number and ISO timestamp, ensuring the serializer serializes fresh data each time. Messages are sent using `Produce()` (fire-and-forget with delivery handler) to avoid `Task` allocation overhead at high volume.
5. **Consumer tests (T2.1--T2.4)** run next, each consuming messages from the topics populated in step 4. In count mode, each run consumes a fixed number of messages. In duration mode, each run consumes continuously for the specified time. Each run uses a unique consumer group (`throughput-test-{TestId}-run-{N}-{guid}`) to read from offset 0.
6. A formatted results table is printed to the console (including actual message count per run).
7. Results are exported to a timestamped CSV file in `bin/Debug/net9.0/results/`.
8. An interactive HTML report with Chart.js charts is generated alongside the CSV.

### Test Output

The console output includes:
- Per-run metrics for each test (msgs/sec, MB/sec, elapsed time, message count, errors)
- Averaged metrics per test
- A summary comparison table across all tests

All output files are written to the `results/` directory under the build output (`bin/Debug/net9.0/results/`):

| File | Format | Description |
|------|--------|-------------|
| `throughput-results-YYYYMMDD-HHmmss.csv` | CSV | Raw metrics for every run plus per-test averages |
| `throughput-report-YYYYMMDD-HHmmss.html` | HTML | Interactive Chart.js report (opens in any browser) |

The HTML report includes:
- **Summary table** with all metrics for every test run
- **Bar charts** comparing msgs/sec and MB/sec across all tests
- **Time-series line charts** showing instantaneous throughput over time (duration mode only), grouped by producer and consumer tests
- Color-coded by test: Avro Small (green), Avro Large (blue), JSON Small (orange), JSON Large (red)

## Project Structure

```
confluent-throughput-test-harness/
├── Program.cs                          # Main orchestrator and CLI
├── ConfluentThroughputTestHarness.csproj
├── appsettings.json                    # Template config (committed)
├── appsettings.Development.json        # Real credentials (gitignored)
├── Schemas/
│   ├── test-avro-small-value.avsc      # Small Avro schema (27 fields)
│   ├── test-avro-large-value.avsc      # Large Avro schema (106 fields)
│   ├── test-json-small-value.json      # Small JSON schema (27 fields)
│   ├── test-json-large-value.json      # Large JSON schema (106 fields)
│   ├── test-avro-key.avsc             # Avro key schema (int)
│   └── test-json-key.json             # JSON key schema (integer)
├── Config/
│   └── Settings.cs                     # KafkaSettings, SchemaRegistrySettings, TestSettings
├── LogicalTypes/
│   ├── VarcharLogicalType.cs           # Custom Avro logical type for varchar
│   └── CharLogicalType.cs             # Custom Avro logical type for char
├── Models/
│   ├── FreightDboTblLoadsSmall.cs      # JSON POCO for small payload (27 fields)
│   └── FreightDboTblLoads.cs           # JSON POCO for large payload (106 fields)
├── DataFactories/
│   ├── ITestDataFactory.cs             # Factory interface
│   ├── AvroSmallDataFactory.cs         # GenericRecord builder (27 fields)
│   ├── AvroLargeDataFactory.cs         # GenericRecord builder (106 fields)
│   ├── JsonSmallDataFactory.cs         # POCO builder (27 fields)
│   └── JsonLargeDataFactory.cs         # POCO builder (106 fields)
├── Tests/
│   ├── TestDefinition.cs               # Test IDs, types, and configuration
│   ├── TestResult.cs                   # Per-run metrics
│   ├── TestSuite.cs                    # Aggregation and averages
│   └── ThroughputSample.cs            # Time-series throughput snapshot
├── Metrics/
│   ├── ResourceMonitor.cs              # CPU/memory sampling (250ms interval)
│   └── ByteCountingDeserializer.cs     # Wraps deserializers for byte tracking
├── Runners/
│   ├── ProducerTestRunner.cs           # Producer benchmark (Avro + JSON)
│   └── ConsumerTestRunner.cs           # Consumer benchmark (Avro + JSON)
└── Reporting/
    ├── ConsoleReporter.cs              # Spectre.Console formatted tables
    ├── CsvReporter.cs                  # CSV export
    └── HtmlChartReporter.cs           # Interactive Chart.js HTML report
```

## Design Decisions

- **GenericRecord for Avro**: The freight schema uses custom logical types (`varchar`, `char`) that are not supported by Avro code generation. `GenericRecord` with `Schema.Parse()` and custom logical type registration handles this cleanly.
- **POCO classes for JSON**: The `Confluent.SchemaRegistry.Serdes.Json` serializer works with plain C# classes annotated with `System.Text.Json` attributes.
- **`Produce()` over `ProduceAsync()`**: The synchronous fire-and-forget `Produce()` with a delivery callback avoids `Task` allocation GC pressure at high message volumes, giving more accurate throughput numbers.
- **Throughput-optimized producer defaults**: `acks=1` (Leader), `linger.ms=100`, `batch.size=1000000` (1 MB), and LZ4 compression maximize batching and reduce round-trips to the broker.
- **Pre-registered schemas**: All 8 schemas (4 value + 4 key) are registered ahead of time. Serializers use `AutoRegisterSchemas = false` and `UseLatestVersion = true` to look up schemas by subject at runtime.
- **Integer keys**: All messages use sequential integer keys (1, 2, 3, ...) serialized with Avro (`AvroSerializer<int>`) for Avro topics or the default `Serializers.Int32` for JSON topics.
- **Per-message uniqueness**: Each message gets a unique `__test_seq` (sequence number) and `__test_ts` (ISO timestamp) stamped into the value before every `Produce()`. This proves the serializer is doing real work on every message, not serving a cached serialization result. One record template is created and mutated in-place to avoid object construction overhead.
- **Dual execution modes**: Count-based mode (default) produces/consumes a fixed number of messages for quick benchmarking. Duration-based mode (`--duration N`) runs each test for N minutes, capturing sustained throughput over longer periods (e.g., 10, 15, or 20 minutes). Both modes coexist and can be selected via CLI or config.
- **Unique consumer group per run**: Each consumer run creates a group ID like `throughput-test-T2.1-run-1-<guid>` so every run reads the full topic from the beginning.
- **Interactive HTML report**: Every test run generates a self-contained HTML file using Chart.js (loaded from CDN). Bar charts compare throughput across all tests at a glance. In duration mode, time-series line charts plot instantaneous throughput (computed from consecutive cumulative samples) over the full run, making it easy to spot warm-up periods, throttling, or throughput degradation.
