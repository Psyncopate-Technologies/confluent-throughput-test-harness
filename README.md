# Confluent Kafka Throughput Test Harness

A .NET 10 console application that benchmarks **Avro vs JSON** serialization throughput against a Confluent Cloud Kafka cluster. The harness includes two producer test suites -- **drag-race tests** (T1.x) comparing raw Produce vs ProduceAsync throughput, and **business-realistic tests** (T3.x) modeling production patterns with `acks=all`, idempotence, `Task.WhenAll` concurrency windows, and fire-and-forget `Produce` with delivery handler callbacks -- plus consumer tests (T2.x). Metrics include messages/sec, MB/sec, average latency, peak CPU, and peak memory usage.

## Test Matrix

The harness runs **32 tests**: 8 drag-race producer tests (T1.1--T1.8), 20 business-realistic producer tests (T3.1--T3.20), and 4 consumer tests (T2.1--T2.4).

Each test run uses **hybrid termination**: it stops when **either** the message count limit (default 100,000) or the time limit (default 1 minute) is reached -- whichever comes first. Each test runs 3 times by default.

### Drag-Race Producer Tests (T1.x)

Compare raw `Produce` (fire-and-forget) vs `ProduceAsync` (await each) throughput across 4 format/size combinations. Uses `acks=Leader` and `EnableIdempotence=false` for maximum speed. Avro uses SpecificRecord only.

| ID | Format | Payload | Record Type | Produce API | Commit Strategy | Topic |
|----|--------|---------|-------------|-------------|-----------------|-------|
| **T1.1** | Avro | Small | SpecificRecord | Produce | Single | `test-avro-small-specificrecord` |
| **T1.2** | Avro | Small | SpecificRecord | ProduceAsync | Single | `test-avro-small-specificrecord` |
| **T1.3** | Avro | Large | SpecificRecord | Produce | Single | `test-avro-large-specificrecord` |
| **T1.4** | Avro | Large | SpecificRecord | ProduceAsync | Single | `test-avro-large-specificrecord` |
| **T1.5** | JSON | Small | N/A | Produce | Single | `test-json-small` |
| **T1.6** | JSON | Small | N/A | ProduceAsync | Single | `test-json-small` |
| **T1.7** | JSON | Large | N/A | Produce | Single | `test-json-large` |
| **T1.8** | JSON | Large | N/A | ProduceAsync | Single | `test-json-large` |

### Business-Realistic Producer Tests (T3.x)

Model production event-driven patterns with guaranteed delivery. All T3.x tests use:
- **`ProduceAsync`** with `acks=all` and `enable.idempotence=true`
- **`Task.WhenAll` concurrency windows**: fire N `ProduceAsync` calls concurrently, await all, check each `DeliveryResult` for errors
- **SpecificRecord** for Avro (matching client's source-generated `ISpecificRecord` classes)

Window sizes 1, 10, 50, and 100 simulate different levels of concurrent in-flight messages, from single-message sequential processing to 100 concurrent background tasks.

| ID | Format | Payload | Window | Name |
|----|--------|---------|--------|------|
| **T3.1** | Avro | Small | 1 | Business Avro Small Window-1 |
| **T3.2** | Avro | Small | 10 | Business Avro Small Window-10 |
| **T3.3** | Avro | Small | 50 | Business Avro Small Window-50 |
| **T3.4** | Avro | Small | 100 | Business Avro Small Window-100 |
| **T3.5** | Avro | Large | 1 | Business Avro Large Window-1 |
| **T3.6** | Avro | Large | 10 | Business Avro Large Window-10 |
| **T3.7** | Avro | Large | 50 | Business Avro Large Window-50 |
| **T3.8** | Avro | Large | 100 | Business Avro Large Window-100 |
| **T3.9** | JSON | Small | 1 | Business JSON Small Window-1 |
| **T3.10** | JSON | Small | 10 | Business JSON Small Window-10 |
| **T3.11** | JSON | Small | 50 | Business JSON Small Window-50 |
| **T3.12** | JSON | Small | 100 | Business JSON Small Window-100 |
| **T3.13** | JSON | Large | 1 | Business JSON Large Window-1 |
| **T3.14** | JSON | Large | 10 | Business JSON Large Window-10 |
| **T3.15** | JSON | Large | 50 | Business JSON Large Window-50 |
| **T3.16** | JSON | Large | 100 | Business JSON Large Window-100 |

### Business-Realistic Delivery Handler Tests (T3.17–T3.20)

Model the fire-and-forget `Produce` pattern with a delivery handler callback for maximum throughput while still processing each delivery result individually. All T3.17–T3.20 tests use:
- **`Produce`** (fire-and-forget) with a **delivery handler callback**
- `acks=all` and `enable.idempotence=true` (same business-realistic config as T3.1–T3.16)
- **SpecificRecord** for Avro (matching client's source-generated `ISpecificRecord` classes)
- No explicit flush inside the loop — librdkafka batches via `linger.ms`/`batch.size`

| ID | Format | Payload | Pattern | Name |
|----|--------|---------|---------|------|
| **T3.17** | Avro | Small | DeliveryHandler | Business Avro Small DeliveryHandler |
| **T3.18** | Avro | Large | DeliveryHandler | Business Avro Large DeliveryHandler |
| **T3.19** | JSON | Small | DeliveryHandler | Business JSON Small DeliveryHandler |
| **T3.20** | JSON | Large | DeliveryHandler | Business JSON Large DeliveryHandler |

### Consumer Tests (T2.x)

Consumer tests read from the topics populated by the producer tests. Each consumer run uses a unique consumer group ID (`throughput-test-{TestId}-run-{N}-{guid}`) to ensure it reads from offset 0.

| ID | Format | Payload | Topic | Runs |
|----|--------|---------|-------|------|
| **T2.1** | Avro | Small (27 fields) | `test-avro-small` | 3 |
| **T2.2** | Avro | Large (106 fields) | `test-avro-large` | 3 |
| **T2.3** | JSON | Small (27 fields) | `test-json-small` | 3 |
| **T2.4** | JSON | Large (106 fields) | `test-json-large` | 3 |

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

When a time limit is configured (default 3 minutes), time-series throughput samples are collected every second, capturing cumulative message counts over time. These samples power the interactive line charts in the HTML report.

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
| `Test` | `MessageCount` | `100000` | Max messages per test run (hybrid mode) |
| `Test` | `DurationMinutes` | `3` | Max minutes per test run (hybrid mode); set to `null` for count-only mode |
| `Test` | `ProducerRuns` | `3` | Number of runs per producer test |
| `Test` | `ConsumerRuns` | `3` | Number of runs per consumer test |
| `Test` | `BusinessRealisticRuns` | `3` | Number of runs per T3.x business-realistic test |
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
| `--producer-only` | Run only producer tests (T1.x and T3.x) |
| `--consumer-only` | Run only consumer tests (T2.1--T2.4) |
| `--test <ID>` | Run a single test by ID (e.g., `T1.1`, `T2.3`) |
| `--test <start>-<end>` | Run a range of tests (e.g., `T1.1-T1.8`) |
| `--test <ID>,<ID>,...` | Run a comma-separated list (e.g., `T1.1,T1.4,T1.8`) |
| `--duration <N>` | Override duration minutes from CLI (e.g., `--duration 10`) |
| `--help` | Show usage information and full test list |

### Execution Scenarios

```bash
# ── Hybrid Mode (default: 100K msgs or 1 min, whichever first) ────

# Run all 32 tests (T1.x drag-race, T3.x business-realistic, T2.x consumers)
dotnet run

# Run only producer tests (T1.x + T3.x)
dotnet run -- --producer-only

# Run only the 4 consumer tests (topics must already contain messages)
dotnet run -- --consumer-only

# Run a single drag-race test
dotnet run -- --test T1.1

# Run all business-realistic Avro Small tests (windows 1, 10, 50, 100)
dotnet run -- --test T3.1-T3.4

# Run specific tests by comma-separated list
dotnet run -- --test T1.1,T3.1,T3.4

# Override the time limit to 10 minutes per run
dotnet run -- --duration 10

# Run only producer tests with a 15-minute time limit each
dotnet run -- --producer-only --duration 15

# ── Count-Only Mode ───────────────────────────────────────────────
# To disable the time limit and use only message count,
# set "DurationMinutes": null in appsettings.json, then:

# Run all 32 tests with 100K messages per run (no time limit)
dotnet run

# ── Help ───────────────────────────────────────────────────────────

# Show usage, all test IDs, and descriptions
dotnet run -- --help
```

### Execution Flow

1. The application loads configuration from `appsettings.json`, `appsettings.Development.json`, and environment variables.
2. Both Avro schemas are parsed from the `Schemas/` directory. Custom logical types (`varchar`, `char`) are registered to handle the freight schema.
3. CLI arguments are parsed to determine which tests to run and whether to override the duration.
4. The full 32-test matrix is generated from `TestDefinition.GetAll()`, then filtered by CLI flags (`--test`, `--producer-only`, `--consumer-only`).
5. **Drag-race producer tests (T1.1--T1.8)** run first with `acks=Leader` and `EnableIdempotence=false`:
   - **Avro SpecificRecord**: Uses `AvroSerializer<T>` with source-generated `ISpecificRecord` implementations.
   - **JSON**: Uses `JsonSerializer<T>` with POCO classes.
   - For `Produce` (fire-and-forget), async serializers are wrapped with `.AsSyncOverAsync()`.
   - For `ProduceAsync`, async serializers are used directly.
   - The unified `RunProducerLoopAsync` handles produce-API branching and commit-strategy flushing.
6. **Business-realistic producer tests (T3.1--T3.16)** run next with `acks=All` and `EnableIdempotence=true`:
   - Fire N `ProduceAsync` calls concurrently via `Task.WhenAll` (window sizes 1, 10, 50, 100).
   - Each `DeliveryResult` is checked for errors after the await.
   - `RunConcurrencyWindowLoopAsync` handles the windowed produce pattern.
   - **T3.17--T3.20** use fire-and-forget `Produce` with a delivery handler callback for maximum throughput.
   - `RunDeliveryHandlerLoopAsync` handles the delivery handler pattern.
7. **Consumer tests (T2.1--T2.4)** run last, each consuming messages from their configured topics with the same hybrid termination logic. Each run uses a unique consumer group (`throughput-test-{TestId}-run-{N}-{guid}`) to read from offset 0.
8. A formatted results table is printed to the console with per-run and averaged metrics, including API, Commit Strategy, Concurrency Window, and Record Type columns.
9. Results are exported to a timestamped CSV file and an interactive HTML report in `bin/Debug/net10.0/results/`.

### Test Output

The console output includes:
- Per-run metrics for each test (msgs/sec, MB/sec, elapsed time, message count, errors)
- Averaged metrics per test
- A summary comparison table across all tests (with Produce API, Commit Strategy, Concurrency Window, and Record Type columns)

All output files are written to the `results/` directory under the build output (`bin/Debug/net10.0/results/`):

| File | Format | Description |
|------|--------|-------------|
| `throughput-results-YYYYMMDD-HHmmss.csv` | CSV | Raw metrics for every run plus per-test averages, including all 5 dimensions |
| `throughput-report-YYYYMMDD-HHmmss.html` | HTML | Interactive Chart.js report (opens in any browser) |

The HTML report includes:
- **Summary table** with all metrics for every test, including API, Commit Strategy, Concurrency Window, and Record Type
- **Bar charts** comparing msgs/sec and MB/sec across all tests
- **Time-series line charts** showing instantaneous throughput over time (when a time limit is configured), grouped by drag-race (T1.x), business-realistic (T3.x), and consumer (T2.x) tests
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
│   ├── TestDefinition.cs                       # Enums, 32-test matrix generation (T1.x + T3.x + T2.x)
│   ├── TestResult.cs                           # Per-run metrics (incl. API, Commit, Window, RecordType)
│   ├── TestSuite.cs                            # Aggregation and averages
│   └── ThroughputSample.cs                     # Time-series throughput snapshot
├── Metrics/
│   ├── ResourceMonitor.cs                      # CPU/memory sampling (250ms interval)
│   └── ByteCountingDeserializer.cs             # Wraps deserializers for byte tracking
├── Runners/
│   ├── ProducerTestRunner.cs                   # Producer benchmark (drag-race + business-realistic)
│   └── ConsumerTestRunner.cs                   # Consumer benchmark (Avro + JSON)
└── Reporting/
    ├── ConsoleReporter.cs                      # Spectre.Console formatted tables
    ├── CsvReporter.cs                          # CSV export with all dimensions
    └── HtmlChartReporter.cs                    # Interactive Chart.js HTML report
```

## Design Decisions

- **SpecificRecord for Avro**: The freight schema uses custom logical types (`varchar`, `char`) that are incompatible with `avrogen` code generation. The `ISpecificRecord` implementations (`FreightSmallSpecific`, `FreightLargeSpecific`) were generated by a .NET Source Generator, using the same `.avsc` schemas and custom logical type registrations.
- **POCO classes for JSON**: The `Confluent.SchemaRegistry.Serdes.Json` serializer works with plain C# classes annotated with `System.Text.Json` attributes.
- **Business-realistic producer tests (T3.x)**: Model production patterns with `acks=All`, `EnableIdempotence=true`, and `Task.WhenAll` concurrency windows.
- **Concurrency window pattern (T3.1–T3.16)**: Fire N `ProduceAsync` calls concurrently, await all with `Task.WhenAll`, then check each `DeliveryResult` for errors. Window sizes 1, 10, 50, and 100 simulate different levels of concurrency, from single sequential messages to 100 concurrent in-flight requests.
- **Delivery handler pattern (T3.17–T3.20)**: Fire-and-forget `Produce` with a delivery handler callback for maximum throughput while still processing each delivery result individually. No explicit flush inside the loop — librdkafka batches internally via `linger.ms`/`batch.size`. A final `Flush(60s)` after the loop drains remaining in-flight messages. Uses the same `acks=all` + `enable.idempotence=true` business-realistic config.
- **Separate producer topics per record type**: Avro SpecificRecord tests write to dedicated topics (e.g., `test-avro-small-specificrecord`) to avoid cross-contamination of benchmark data.
- **Pre-registered schemas**: All schemas are registered ahead of time. Serializers use `AutoRegisterSchemas = false` and `UseLatestVersion = true` to look up schemas by subject at runtime.
- **Integer keys**: All messages use sequential integer keys (1, 2, 3, ...) serialized with `AvroSerializer<int>` for Avro topics or `JsonSerializer<T>` / default `Serializers.Int32` for JSON topics.
- **Per-message uniqueness**: Each message gets a unique `__test_seq` (sequence number) and `__test_ts` (ISO timestamp) stamped into the value before every produce call. This proves the serializer is doing real work on every message. One record template is created and mutated in-place to avoid object construction overhead.
- **Hybrid termination**: Each test run stops when either the message count limit (default 100K) or the time limit (default 3 minutes) is reached, whichever comes first. Fast tests (e.g., Batch5K) hit the message count in seconds; slow tests (e.g., Single commit) are capped by the time limit. Set `DurationMinutes` to `null` for count-only mode.
- **Unique consumer group per run**: Each consumer run creates a group ID like `throughput-test-T2.1-run-1-<guid>` so every run reads the full topic from the beginning. Note: The API key used must have permissions to dynamically register new consumer groups in Confluent Cloud.
- **Interactive HTML report**: Every test run generates a self-contained HTML file using Chart.js (loaded from CDN). Bar charts compare throughput across all tests at a glance. When a time limit is configured, time-series line charts plot instantaneous throughput over the full run, making it easy to spot warm-up periods, throttling, or throughput degradation.
