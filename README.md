# Confluent Kafka Throughput Test Harness

A .NET 10 console application that benchmarks **Avro vs JSON** serialization throughput against a Confluent Cloud Kafka cluster. The harness measures producer and consumer performance across **5 dimensions** -- serialization format, payload size, record type, produce API, and commit strategy -- producing detailed metrics including messages/sec, MB/sec, average latency, peak CPU, and peak memory usage.

## Test Matrix

The harness runs **28 tests**: 24 producer tests (T1.1--T1.24) and 4 consumer tests (T2.1--T2.4). It supports two execution modes:

- **Duration-based (default)**: Each test runs for a specified number of minutes (default 3 minutes), producing or consuming as many messages as possible within that time window.
- **Count-based**: Each test produces or consumes a fixed number of messages (default 100,000). Set `DurationMinutes` to `null` in `appsettings.json` to use this mode.

### Five Test Dimensions (Producers)

| Dimension | Values |
|---|---|
| **Serialization** | Avro, JSON |
| **Payload Size** | Small (27 fields), Large (106 fields) |
| **Record Type** | `SpecificRecord`, `GenericRecord` (Avro only); N/A for JSON |
| **Produce API** | `Produce` (sync fire-and-forget with delivery handler), `ProduceAsync` (await each broker ack) |
| **Commit Strategy** | Single (flush after every message), BatchConfigurable (flush every `BatchCommitSize` msgs, default 5000), Batch5K (flush every 5,000 msgs) |

### Producer Tests (T1.x)

Each group of 4 tests shares the same serialization/payload/record-type combination and varies only by produce API and commit strategy.

| ID | Format | Payload | Record Type | Produce API | Commit Strategy | Topic |
|----|--------|---------|-------------|-------------|-----------------|-------|
| **T1.1** | Avro | Small | SpecificRecord | Produce | Single | `test-avro-small-specificrecord` |
| **T1.2** | Avro | Small | SpecificRecord | ProduceAsync | Single | `test-avro-small-specificrecord` |
| **T1.3** | Avro | Small | SpecificRecord | ProduceAsync | BatchConfigurable | `test-avro-small-specificrecord` |
| **T1.4** | Avro | Small | SpecificRecord | Produce | Batch5K | `test-avro-small-specificrecord` |
| **T1.5** | Avro | Small | GenericRecord | Produce | Single | `test-avro-small-genericrecord` |
| **T1.6** | Avro | Small | GenericRecord | ProduceAsync | Single | `test-avro-small-genericrecord` |
| **T1.7** | Avro | Small | GenericRecord | ProduceAsync | BatchConfigurable | `test-avro-small-genericrecord` |
| **T1.8** | Avro | Small | GenericRecord | Produce | Batch5K | `test-avro-small-genericrecord` |
| **T1.9** | Avro | Large | SpecificRecord | Produce | Single | `test-avro-large-specificrecord` |
| **T1.10** | Avro | Large | SpecificRecord | ProduceAsync | Single | `test-avro-large-specificrecord` |
| **T1.11** | Avro | Large | SpecificRecord | ProduceAsync | BatchConfigurable | `test-avro-large-specificrecord` |
| **T1.12** | Avro | Large | SpecificRecord | Produce | Batch5K | `test-avro-large-specificrecord` |
| **T1.13** | Avro | Large | GenericRecord | Produce | Single | `test-avro-large-genericrecord` |
| **T1.14** | Avro | Large | GenericRecord | ProduceAsync | Single | `test-avro-large-genericrecord` |
| **T1.15** | Avro | Large | GenericRecord | ProduceAsync | BatchConfigurable | `test-avro-large-genericrecord` |
| **T1.16** | Avro | Large | GenericRecord | Produce | Batch5K | `test-avro-large-genericrecord` |
| **T1.17** | JSON | Small | N/A | Produce | Single | `test-json-small` |
| **T1.18** | JSON | Small | N/A | ProduceAsync | Single | `test-json-small` |
| **T1.19** | JSON | Small | N/A | ProduceAsync | BatchConfigurable | `test-json-small` |
| **T1.20** | JSON | Small | N/A | Produce | Batch5K | `test-json-small` |
| **T1.21** | JSON | Large | N/A | Produce | Single | `test-json-large` |
| **T1.22** | JSON | Large | N/A | ProduceAsync | Single | `test-json-large` |
| **T1.23** | JSON | Large | N/A | ProduceAsync | BatchConfigurable | `test-json-large` |
| **T1.24** | JSON | Large | N/A | Produce | Batch5K | `test-json-large` |

### Consumer Tests (T2.x)

Consumer tests read from the topics populated by the producer tests. Each consumer run uses a unique consumer group ID (`throughput-test-{TestId}-run-{N}-{guid}`) to ensure it reads from offset 0.

| ID | Format | Payload | Topic | Runs |
|----|--------|---------|-------|------|
| **T2.1** | Avro | Small (27 fields) | `test-avro-small` | 5 |
| **T2.2** | Avro | Large (106 fields) | `test-avro-large` | 5 |
| **T2.3** | JSON | Small (27 fields) | `test-json-small` | 5 |
| **T2.4** | JSON | Large (106 fields) | `test-json-large` | 5 |

## Schemas

Both value schemas are derived from a production freight CDC (`tblloads`) table. The small schema is a 27-field subset of the full 106-field large schema, covering a representative mix of data types: integers, strings (varchar/char), booleans, timestamps, and decimals. All message keys are integers.

Each schema includes two test header fields (`__test_seq` and `__test_ts`) embedded in the value payload. These are stamped with a unique sequence number and ISO timestamp before every produce call, ensuring each message has unique content and proving the serializer performs real serialization work on every message.

Each schema has both an Avro (`.avsc`) and a JSON Schema (`.json`) variant in the `Schemas/` directory, named to match their Schema Registry subject:

### Value Schemas

| File | SR Subject | Fields |
|------|------------|--------|
| `test-avro-small-value.avsc` | `test-avro-small-value` | 27 |
| `test-avro-large-value.avsc` | `test-avro-large-value` | 106 |
| `test-json-small-value.json` | `test-json-small-value` | 27 |
| `test-json-large-value.json` | `test-json-large-value` | 106 |

### Key Schemas

| File | SR Subjects | Type |
|------|-------------|------|
| `test-avro-key.avsc` | `test-avro-small-key`, `test-avro-large-key` | Avro `int` |
| `test-json-key.json` | `test-json-small-key`, `test-json-large-key` | JSON `integer` |

## Metrics Collected

For each test run, the harness captures:

- **Messages/sec** -- end-to-end throughput
- **MB/sec** -- data throughput based on serialized message size
- **Average latency (ms)** -- elapsed time / message count
- **Peak CPU %** -- sampled every 250ms during the run
- **Peak memory (MB)** -- working set high watermark
- **Delivery errors** -- count of failed produce/consume operations

In duration mode, time-series throughput samples are collected every second, capturing cumulative message counts over time. These samples power the interactive line charts in the HTML report.

Results are displayed in a formatted console table (with API, Commit Strategy, and Record Type columns) and exported to both CSV and an interactive HTML report.

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

Create the test topics. The harness expects 1 partition per topic (to avoid partition-level variability in benchmarks) with a short retention. Avro producer tests use separate topics per record type to avoid cross-contamination:

```bash
# Avro producer topics (separate per record type)
confluent kafka topic create test-avro-small-specificrecord --partitions 1 --config "retention.ms=3600000"
confluent kafka topic create test-avro-small-genericrecord  --partitions 1 --config "retention.ms=3600000"
confluent kafka topic create test-avro-large-specificrecord --partitions 1 --config "retention.ms=3600000"
confluent kafka topic create test-avro-large-genericrecord  --partitions 1 --config "retention.ms=3600000"

# JSON producer topics
confluent kafka topic create test-json-small --partitions 1 --config "retention.ms=3600000"
confluent kafka topic create test-json-large --partitions 1 --config "retention.ms=3600000"

# Consumer topics (shared Avro topics for T2.x consumer tests)
confluent kafka topic create test-avro-small --partitions 1 --config "retention.ms=3600000"
confluent kafka topic create test-avro-large --partitions 1 --config "retention.ms=3600000"
```

### 4. Schema Registration

All schemas must be pre-registered. The serializers are configured with `AutoRegisterSchemas = false` and `UseLatestVersion = true`.

```bash
# Value schemas — register for each topic subject
confluent schema-registry schema create \
  --subject test-avro-small-specificrecord-value \
  --schema Schemas/test-avro-small-value.avsc --type avro

confluent schema-registry schema create \
  --subject test-avro-small-genericrecord-value \
  --schema Schemas/test-avro-small-value.avsc --type avro

confluent schema-registry schema create \
  --subject test-avro-large-specificrecord-value \
  --schema Schemas/test-avro-large-value.avsc --type avro

confluent schema-registry schema create \
  --subject test-avro-large-genericrecord-value \
  --schema Schemas/test-avro-large-value.avsc --type avro

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

# Key schemas — register for each topic subject
for topic in test-avro-small-specificrecord test-avro-small-genericrecord \
             test-avro-large-specificrecord test-avro-large-genericrecord \
             test-avro-small test-avro-large; do
  confluent schema-registry schema create \
    --subject "${topic}-key" \
    --schema Schemas/test-avro-key.avsc --type avro
done

for topic in test-json-small test-json-large; do
  confluent schema-registry schema create \
    --subject "${topic}-key" \
    --schema Schemas/test-json-key.json --type json
done
```

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
| `Test` | `DurationMinutes` | `3` | Minutes per test run (duration mode); set to `null` for count mode |
| `Test` | `ProducerRuns` | `3` | Number of runs per producer test |
| `Test` | `ConsumerRuns` | `5` | Number of runs per consumer test |
| `Test` | `BatchCommitSize` | `5000` | Flush interval for `BatchConfigurable` commit strategy |
| `Test` | `AvroSmallTopic` | `test-avro-small` | Consumer topic for small Avro payloads |
| `Test` | `AvroLargeTopic` | `test-avro-large` | Consumer topic for large Avro payloads |
| `Test` | `AvroSmallSpecificTopic` | `test-avro-small-specificrecord` | Producer topic for Avro small SpecificRecord |
| `Test` | `AvroSmallGenericTopic` | `test-avro-small-genericrecord` | Producer topic for Avro small GenericRecord |
| `Test` | `AvroLargeSpecificTopic` | `test-avro-large-specificrecord` | Producer topic for Avro large SpecificRecord |
| `Test` | `AvroLargeGenericTopic` | `test-avro-large-genericrecord` | Producer topic for Avro large GenericRecord |
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
| `--producer-only` | Run only producer tests (T1.1--T1.24) |
| `--consumer-only` | Run only consumer tests (T2.1--T2.4) |
| `--test <ID>` | Run a single test by ID (e.g., `T1.1`, `T2.3`) |
| `--test <start>-<end>` | Run a range of tests (e.g., `T1.1-T1.8`) |
| `--duration <N>` | Override duration minutes from CLI (e.g., `--duration 10`) |
| `--help` | Show usage information and full test list |

### Execution Scenarios

```bash
# ── Duration Mode (default: 3 minutes per run) ─────────────────────

# Run all 28 tests (T1.1-T1.24 producers, then T2.1-T2.4 consumers)
dotnet run

# Run only the 24 producer tests
dotnet run -- --producer-only

# Run only the 4 consumer tests (topics must already contain messages)
dotnet run -- --consumer-only

# Run a single producer test
dotnet run -- --test T1.1

# Run a range of tests (e.g., all Avro Small Specific + Generic)
dotnet run -- --test T1.1-T1.8

# Override duration to 10 minutes per run
dotnet run -- --duration 10

# Run only producer tests for 15 minutes each
dotnet run -- --producer-only --duration 15

# ── Count Mode ─────────────────────────────────────────────────────
# To use count mode, set "DurationMinutes": null in appsettings.json
# or appsettings.Development.json, then:

# Run all 28 tests with 100K messages per run
dotnet run

# ── Help ───────────────────────────────────────────────────────────

# Show usage, all test IDs, and descriptions
dotnet run -- --help
```

### Execution Flow

1. The application loads configuration from `appsettings.json`, `appsettings.Development.json`, and environment variables.
2. Both Avro schemas are parsed from the `Schemas/` directory. Custom logical types (`varchar`, `char`) are registered to handle the freight schema.
3. CLI arguments are parsed to determine which tests to run and whether to override the duration.
4. The full 28-test matrix is generated from `TestDefinition.GetAll()`, then filtered by CLI flags (`--test`, `--producer-only`, `--consumer-only`).
5. **Producer tests (T1.1--T1.24)** run first. Each test is routed to the correct setup method based on its format/size/record-type combination:
   - **Avro SpecificRecord**: Uses `AvroSerializer<FreightSmallSpecific>` or `AvroSerializer<FreightLargeSpecific>` with hand-written `ISpecificRecord` implementations.
   - **Avro GenericRecord**: Uses `AvroSerializer<GenericRecord>` with the parsed `.avsc` schemas.
   - **JSON**: Uses `JsonSerializer<T>` with POCO classes.
   - For `Produce` (fire-and-forget) tests, async serializers are wrapped with `.AsSyncOverAsync()`.
   - For `ProduceAsync` tests, async serializers are used directly (`IAsyncSerializer<T>`).
   - The unified `RunProducerLoopAsync` handles produce-API branching and commit-strategy flushing.
6. **Consumer tests (T2.1--T2.4)** run next, each consuming messages from their configured topics. Each run uses a unique consumer group (`throughput-test-{TestId}-run-{N}-{guid}`) to read from offset 0.
7. A formatted results table is printed to the console with per-run and averaged metrics, including API, Commit Strategy, and Record Type columns.
8. Results are exported to a timestamped CSV file and an interactive HTML report in `bin/Debug/net10.0/results/`.

### Test Output

The console output includes:
- Per-run metrics for each test (msgs/sec, MB/sec, elapsed time, message count, errors)
- Averaged metrics per test
- A summary comparison table across all tests (with Produce API, Commit Strategy, and Record Type columns)

All output files are written to the `results/` directory under the build output (`bin/Debug/net10.0/results/`):

| File | Format | Description |
|------|--------|-------------|
| `throughput-results-YYYYMMDD-HHmmss.csv` | CSV | Raw metrics for every run plus per-test averages, including all 5 dimensions |
| `throughput-report-YYYYMMDD-HHmmss.html` | HTML | Interactive Chart.js report (opens in any browser) |

The HTML report includes:
- **Summary table** with all metrics for every test, including API, Commit Strategy, and Record Type
- **Bar charts** comparing msgs/sec and MB/sec across all tests
- **Time-series line charts** showing instantaneous throughput over time (duration mode only), grouped by producer and consumer tests
- Color-coded by test for easy visual comparison

## Project Structure

```
confluent-throughput-test-harness/
├── Program.cs                                  # Main orchestrator, CLI parsing, test loop
├── ConfluentThroughputTestHarness.csproj
├── appsettings.json                            # Template config (committed)
├── appsettings.Development.json                # Real credentials (gitignored)
├── Schemas/
│   ├── test-avro-small-value.avsc              # Small Avro schema (27 fields)
│   ├── test-avro-large-value.avsc              # Large Avro schema (106 fields)
│   ├── test-json-small-value.json              # Small JSON schema (27 fields)
│   ├── test-json-large-value.json              # Large JSON schema (106 fields)
│   ├── test-avro-key.avsc                      # Avro key schema (int)
│   └── test-json-key.json                      # JSON key schema (integer)
├── Config/
│   └── Settings.cs                             # KafkaSettings, SchemaRegistrySettings, TestSettings
├── LogicalTypes/
│   ├── VarcharLogicalType.cs                   # Custom Avro logical type for varchar
│   └── CharLogicalType.cs                      # Custom Avro logical type for char
├── Models/
│   ├── FreightDboTblLoadsSmall.cs              # JSON POCO for small payload (27 fields)
│   ├── FreightDboTblLoads.cs                   # JSON POCO for large payload (106 fields)
│   └── AvroSpecific/
│       ├── FreightSmallSpecific.cs             # ISpecificRecord for small Avro (27 fields)
│       └── FreightLargeSpecific.cs             # ISpecificRecord for large Avro (106 fields)
├── DataFactories/
│   ├── ITestDataFactory.cs                     # Factory interface (CreateRecord, SetMessageHeader)
│   ├── AvroSmallDataFactory.cs                 # GenericRecord builder (27 fields)
│   ├── AvroLargeDataFactory.cs                 # GenericRecord builder (106 fields)
│   ├── AvroSmallSpecificDataFactory.cs         # FreightSmallSpecific builder
│   ├── AvroLargeSpecificDataFactory.cs         # FreightLargeSpecific builder
│   ├── JsonSmallDataFactory.cs                 # POCO builder (27 fields)
│   └── JsonLargeDataFactory.cs                 # POCO builder (106 fields)
├── Tests/
│   ├── TestDefinition.cs                       # Enums, 28-test matrix generation
│   ├── TestResult.cs                           # Per-run metrics (incl. API, Commit, RecordType)
│   ├── TestSuite.cs                            # Aggregation and averages
│   └── ThroughputSample.cs                     # Time-series throughput snapshot
├── Metrics/
│   ├── ResourceMonitor.cs                      # CPU/memory sampling (250ms interval)
│   └── ByteCountingDeserializer.cs             # Wraps deserializers for byte tracking
├── Runners/
│   ├── ProducerTestRunner.cs                   # Unified producer benchmark (all 5 dimensions)
│   └── ConsumerTestRunner.cs                   # Consumer benchmark (Avro + JSON)
└── Reporting/
    ├── ConsoleReporter.cs                      # Spectre.Console formatted tables
    ├── CsvReporter.cs                          # CSV export with all dimensions
    └── HtmlChartReporter.cs                    # Interactive Chart.js HTML report
```

## Design Decisions

- **SpecificRecord + GenericRecord for Avro**: The freight schema uses custom logical types (`varchar`, `char`) that are incompatible with `avrogen` code generation. Hand-written `ISpecificRecord` implementations (`FreightSmallSpecific`, `FreightLargeSpecific`) enable SpecificRecord benchmarking alongside GenericRecord, using the same `.avsc` schemas and custom logical type registrations.
- **POCO classes for JSON**: The `Confluent.SchemaRegistry.Serdes.Json` serializer works with plain C# classes annotated with `System.Text.Json` attributes.
- **Produce vs ProduceAsync**: The synchronous `Produce()` with a delivery callback avoids `Task` allocation overhead at high message volumes. `ProduceAsync()` awaits each broker acknowledgment, giving different throughput characteristics. Both are benchmarked.
- **Three commit strategies**: Single (flush every message) measures worst-case latency. BatchConfigurable (flush every `BatchCommitSize` messages) is tunable. Batch5K (flush every 5,000) provides a fixed baseline.
- **Unified produce loop**: `RunProducerLoopAsync<TValue>` handles all 5 dimensions in a single generic method. The caller sets up the producer with the right serializers; the loop branches on `ProduceApi` and flushes per the `batchSize` derived from `CommitStrategy`.
- **Separate producer topics per record type**: Avro SpecificRecord and GenericRecord tests write to separate topics (e.g., `test-avro-small-specificrecord` vs `test-avro-small-genericrecord`) to avoid cross-contamination of benchmark data.
- **Throughput-optimized producer defaults**: `acks=1` (Leader), `linger.ms=100`, `batch.size=1000000` (1 MB), and LZ4 compression maximize batching and reduce round-trips to the broker.
- **Pre-registered schemas**: All schemas are registered ahead of time. Serializers use `AutoRegisterSchemas = false` and `UseLatestVersion = true` to look up schemas by subject at runtime.
- **Integer keys**: All messages use sequential integer keys (1, 2, 3, ...) serialized with `AvroSerializer<int>` for Avro topics or `JsonSerializer<T>` / default `Serializers.Int32` for JSON topics.
- **Per-message uniqueness**: Each message gets a unique `__test_seq` (sequence number) and `__test_ts` (ISO timestamp) stamped into the value before every produce call. This proves the serializer is doing real work on every message. One record template is created and mutated in-place to avoid object construction overhead.
- **Dual execution modes**: Duration-based mode (default, 3 minutes) captures sustained throughput over longer periods. Count-based mode produces/consumes a fixed number of messages for quick benchmarking. Both modes coexist and can be selected via config or CLI.
- **Unique consumer group per run**: Each consumer run creates a group ID like `throughput-test-T2.1-run-1-<guid>` so every run reads the full topic from the beginning.
- **Interactive HTML report**: Every test run generates a self-contained HTML file using Chart.js (loaded from CDN). Bar charts compare throughput across all tests at a glance. In duration mode, time-series line charts plot instantaneous throughput over the full run, making it easy to spot warm-up periods, throttling, or throughput degradation.
