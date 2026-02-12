# Confluent Kafka Throughput Test Harness

A .NET 10 console application that benchmarks **Avro vs JSON** serialization throughput against a Confluent Cloud Kafka cluster. The harness includes three scenario-based producer test suites -- **Fire-and-Forget** (T1.x), **Request-Response** (T2.x), and **Batch Processing** (T3.x) -- plus consumer tests (T4.x) with manual offset commit strategies. All producer tests use `acks=all` + `enable.idempotence=true`. Metrics include messages/sec, MB/sec, average latency, peak CPU, and peak memory usage.

## Test Matrix

The harness runs **28 tests**: 4 fire-and-forget producer tests (T1.1--T1.4), 4 request-response producer tests (T2.1--T2.4), 12 batch processing producer tests (T3.1--T3.12), and 8 consumer tests (T4.1--T4.8).

Each test run uses **hybrid termination**: it stops when **either** the message count limit (default 100,000) or the time limit (default 1 minute) is reached -- whichever comes first. Each test runs 3 times by default.

### Fire-and-Forget Producer Tests (T1.x)

Maximum throughput pattern using `Produce` (fire-and-forget) with a delivery handler callback. librdkafka batches internally via `linger.ms`/`batch.size`. A final `Flush(60s)` after the loop drains remaining in-flight messages.

| ID | Format | Payload | Pattern | Topic |
|----|--------|---------|---------|-------|
| **T1.1** | Avro | Small | Produce + delivery handler | `test-avro-small-specificrecord` |
| **T1.2** | Avro | Large | Produce + delivery handler | `test-avro-large-specificrecord` |
| **T1.3** | JSON | Small | Produce + delivery handler | `test-json-small` |
| **T1.4** | JSON | Large | Produce + delivery handler | `test-json-large` |

### Request-Response Producer Tests (T2.x)

Synchronous pattern using `ProduceAsync` + await each message individually. Models request-response patterns where the caller needs confirmation before proceeding.

| ID | Format | Payload | Pattern | Topic |
|----|--------|---------|---------|-------|
| **T2.1** | Avro | Small | ProduceAsync + await each | `test-avro-small-specificrecord` |
| **T2.2** | Avro | Large | ProduceAsync + await each | `test-avro-large-specificrecord` |
| **T2.3** | JSON | Small | ProduceAsync + await each | `test-json-small` |
| **T2.4** | JSON | Large | ProduceAsync + await each | `test-json-large` |

### Batch Processing Producer Tests (T3.x)

Concurrent pattern using `ProduceAsync` + `Task.WhenAll` concurrency windows. Fire N `ProduceAsync` calls concurrently, await all, check each `DeliveryResult` for errors. Window sizes 1, 10, and 100 simulate different levels of concurrent in-flight messages, from single sequential messages to 100 concurrent background tasks. Batch collection uses a time-bound deadline (`BatchTimeoutSeconds`, default 5s) to prevent indefinite waiting when fewer events arrive than the window size.

| ID | Format | Payload | Window | Name |
|----|--------|---------|--------|------|
| **T3.1** | Avro | Small | 1 | Batch Avro Small Window-1 |
| **T3.2** | Avro | Small | 10 | Batch Avro Small Window-10 |
| **T3.3** | Avro | Small | 100 | Batch Avro Small Window-100 |
| **T3.4** | Avro | Large | 1 | Batch Avro Large Window-1 |
| **T3.5** | Avro | Large | 10 | Batch Avro Large Window-10 |
| **T3.6** | Avro | Large | 100 | Batch Avro Large Window-100 |
| **T3.7** | JSON | Small | 1 | Batch JSON Small Window-1 |
| **T3.8** | JSON | Small | 10 | Batch JSON Small Window-10 |
| **T3.9** | JSON | Small | 100 | Batch JSON Small Window-100 |
| **T3.10** | JSON | Large | 1 | Batch JSON Large Window-1 |
| **T3.11** | JSON | Large | 10 | Batch JSON Large Window-10 |
| **T3.12** | JSON | Large | 100 | Batch JSON Large Window-100 |

### Consumer Tests (T4.x)

Consumer tests read from the topics populated by the producer tests. Each consumer run uses a unique consumer group ID (`throughput-test-{TestId}-run-{N}-{guid}`) to ensure it reads from offset 0. All consumer tests use manual offset commit (`EnableAutoCommit = false`) with two strategies:

- **ManualPerMessage** -- `consumer.Commit(result)` after every message. Strictest guarantee: no message is marked committed until processed.
- **ManualBatch** -- `consumer.Commit()` every N messages (default 100, configurable via `CommitBatchSize`). Throughput-friendly batch commit that reduces broker round-trips.

A final `consumer.Commit()` runs before `consumer.Close()` to flush any remaining uncommitted offsets.

| ID | Format | Payload | Commit Strategy | Batch Size | Topic |
|----|--------|---------|-----------------|------------|-------|
| **T4.1** | Avro | Small (27 fields) | ManualPerMessage | -- | `test-avro-small-specificrecord` |
| **T4.2** | Avro | Large (106 fields) | ManualPerMessage | -- | `test-avro-large-specificrecord` |
| **T4.3** | JSON | Small (27 fields) | ManualPerMessage | -- | `test-json-small` |
| **T4.4** | JSON | Large (106 fields) | ManualPerMessage | -- | `test-json-large` |
| **T4.5** | Avro | Small (27 fields) | ManualBatch | 100 | `test-avro-small-specificrecord` |
| **T4.6** | Avro | Large (106 fields) | ManualBatch | 100 | `test-avro-large-specificrecord` |
| **T4.7** | JSON | Small (27 fields) | ManualBatch | 100 | `test-json-small` |
| **T4.8** | JSON | Large (106 fields) | ManualBatch | 100 | `test-json-large` |

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

When a time limit is configured (default 1 minute), time-series throughput samples are collected every second, capturing cumulative message counts over time. These samples power the interactive line charts in the HTML report.

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

Create the test topics. The harness expects 1 partition per topic (to avoid partition-level variability in benchmarks) with a short retention:

```bash
# Avro topics (shared by producer and consumer tests)
confluent kafka topic create test-avro-small-specificrecord --partitions 1 --config "retention.ms=3600000"
confluent kafka topic create test-avro-large-specificrecord --partitions 1 --config "retention.ms=3600000"

# JSON topics (shared by producer and consumer tests)
confluent kafka topic create test-json-small --partitions 1 --config "retention.ms=3600000"
confluent kafka topic create test-json-large --partitions 1 --config "retention.ms=3600000"
```

### 4. Schema Registration

All schemas must be pre-registered. The serializers are configured with `AutoRegisterSchemas = false` and `UseLatestVersion = true`.

```bash
# Value schemas — register for each topic subject
confluent schema-registry schema create \
  --subject test-avro-small-specificrecord-value \
  --schema Schemas/test-avro-small-value.avsc --type avro

confluent schema-registry schema create \
  --subject test-avro-large-specificrecord-value \
  --schema Schemas/test-avro-large-value.avsc --type avro

confluent schema-registry schema create \
  --subject test-json-small-value \
  --schema Schemas/test-json-small-value.json --type json

confluent schema-registry schema create \
  --subject test-json-large-value \
  --schema Schemas/test-json-large-value.json --type json

# Key schemas — register for each topic subject
for topic in test-avro-small-specificrecord \
             test-avro-large-specificrecord; do
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
| `Kafka` | `Acks` | `Leader` | Producer acknowledgment level (ignored; all tests use `All`) |
| `Kafka` | `LingerMs` | `100` | Producer linger time in milliseconds |
| `Kafka` | `BatchSize` | `1000000` | Producer batch size in bytes |
| `Kafka` | `CompressionType` | `Lz4` | Producer compression (`None`, `Gzip`, `Snappy`, `Lz4`, `Zstd`) |
| `SchemaRegistry` | `Url` | -- | Schema Registry endpoint URL |
| `SchemaRegistry` | `BasicAuthUserInfo` | -- | `API_KEY:API_SECRET` format |
| `Test` | `MessageCount` | `100000` | Max messages per test run (hybrid mode) |
| `Test` | `DurationMinutes` | `1` | Max minutes per test run (hybrid mode); set to `null` for count-only mode |
| `Test` | `ProducerRuns` | `3` | Number of runs per producer test |
| `Test` | `ConsumerRuns` | `3` | Number of runs per consumer test |
| `Test` | `BatchTimeoutSeconds` | `5` | Time-bound deadline (seconds) for batch event collection in T3.x Task.WhenAll loop |
| `Test` | `CommitBatchSize` | `100` | Number of messages between offset commits in ManualBatch consumer tests (T4.5--T4.8) |
| `Test` | `AvroSmallSpecificTopic` | `test-avro-small-specificrecord` | Topic for Avro small SpecificRecord (producer + consumer) |
| `Test` | `AvroLargeSpecificTopic` | `test-avro-large-specificrecord` | Topic for Avro large SpecificRecord (producer + consumer) |
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
| `--producer-only` | Run only producer tests (T1.x--T3.x) |
| `--consumer-only` | Run only consumer tests (T4.1--T4.8) |
| `--test <ID>` | Run a single test by ID (e.g., `T1.1`, `T4.3`) |
| `--test <start>-<end>` | Run a range of tests (e.g., `T3.1-T3.12`) |
| `--test <ID>,<ID>,...` | Run a comma-separated list (e.g., `T1.1,T2.1,T3.1`) |
| `--duration <N>` | Override duration minutes from CLI (e.g., `--duration 10`) |
| `--help` | Show usage information and full test list |

### Execution Scenarios

```bash
# ── Hybrid Mode (default: 100K msgs or 1 min, whichever first) ────

# Run all 28 tests (T1.x fire-and-forget, T2.x request-response, T3.x batch, T4.x consumers)
dotnet run

# Run only producer tests (T1.x + T2.x + T3.x)
dotnet run -- --producer-only

# Run only the 8 consumer tests (topics must already contain messages)
dotnet run -- --consumer-only

# Run a single fire-and-forget test
dotnet run -- --test T1.1

# Run a single request-response test
dotnet run -- --test T2.1

# Run all batch processing tests
dotnet run -- --test T3.1-T3.12

# Run specific tests by comma-separated list
dotnet run -- --test T1.1,T2.1,T3.1

# Override the time limit to 10 minutes per run
dotnet run -- --duration 10

# Run only producer tests with a 15-minute time limit each
dotnet run -- --producer-only --duration 15

# ── Count-Only Mode ───────────────────────────────────────────────
# To disable the time limit and use only message count,
# set "DurationMinutes": null in appsettings.json, then:

# Run all 28 tests with 100K messages per run (no time limit)
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
5. **Fire-and-Forget producer tests (T1.1--T1.4)** use `Produce` (fire-and-forget) with a delivery handler callback + `acks=all` + `enable.idempotence=true`.
6. **Request-Response producer tests (T2.1--T2.4)** use `ProduceAsync` + await each message individually + `acks=all` + `enable.idempotence=true`.
7. **Batch Processing producer tests (T3.1--T3.12)** fire N `ProduceAsync` calls concurrently via `Task.WhenAll` (window sizes 1, 10, 100) + `acks=all` + `enable.idempotence=true`. Batch collection uses a time-bound deadline to prevent indefinite waiting.
8. **Consumer tests (T4.1--T4.8)** run last, each consuming messages from their configured topics with the same hybrid termination logic. Each run uses a unique consumer group (`throughput-test-{TestId}-run-{N}-{guid}`) to read from offset 0. All consumer tests use manual offset commit (`EnableAutoCommit = false`): T4.1--T4.4 commit after every message (ManualPerMessage), T4.5--T4.8 commit every N messages (ManualBatch).
9. A formatted results table is printed to the console with per-run and averaged metrics, including API, Commit Strategy, Concurrency Window, and Record Type columns.
10. Results are exported to a timestamped CSV file and an interactive HTML report in `bin/Debug/net10.0/results/`.

### Test Output

The console output includes:
- Per-run metrics for each test (msgs/sec, MB/sec, elapsed time, message count, errors)
- Averaged metrics per test
- A summary comparison table across all tests (with Produce API, Commit Strategy, Concurrency Window, and Record Type columns)

All output files are written to the `results/` directory under the build output (`bin/Debug/net10.0/results/`):

| File | Format | Description |
|------|--------|-------------|
| `throughput-results-YYYYMMDD-HHmmss.csv` | CSV | Raw metrics for every run plus per-test averages |
| `throughput-report-YYYYMMDD-HHmmss.html` | HTML | Interactive Chart.js report (opens in any browser) |

The HTML report includes:
- **Summary table** with all metrics for every test, including API, Commit Strategy, Concurrency Window, and Record Type
- **Bar charts** comparing msgs/sec and MB/sec across all tests
- **Time-series line charts** showing instantaneous throughput over time (when a time limit is configured), grouped by fire-and-forget (T1.x), request-response (T2.x), batch (T3.x), and consumer (T4.x) tests
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
│   ├── TestDefinition.cs                       # Enums, 28-test matrix generation (T1.x–T4.x)
│   ├── TestResult.cs                           # Per-run metrics (incl. API, Commit, Window, RecordType)
│   ├── TestSuite.cs                            # Aggregation and averages
│   └── ThroughputSample.cs                     # Time-series throughput snapshot
├── Metrics/
│   ├── ResourceMonitor.cs                      # CPU/memory sampling (250ms interval)
│   └── ByteCountingDeserializer.cs             # Wraps deserializers for byte tracking
├── Runners/
│   ├── ProducerTestRunner.cs                   # Producer benchmark (fire-and-forget, request-response, batch)
│   └── ConsumerTestRunner.cs                   # Consumer benchmark (Avro + JSON, manual commit)
└── Reporting/
    ├── ConsoleReporter.cs                      # Spectre.Console formatted tables
    ├── CsvReporter.cs                          # CSV export with all dimensions
    └── HtmlChartReporter.cs                    # Interactive Chart.js HTML report
```

## Design Decisions

- **All producers use `acks=all` + `enable.idempotence=true`** -- guaranteed delivery with exactly-once semantics across all three producer scenarios. No more drag-race config with `acks=Leader`.
- **SpecificRecord only for Avro** -- GenericRecord removed (negligible performance difference in benchmarks). The freight schema uses custom logical types (`varchar`, `char`) that are incompatible with `avrogen` code generation. The `ISpecificRecord` implementations (`FreightSmallSpecific`, `FreightLargeSpecific`) were generated by a .NET Source Generator.
- **POCO classes for JSON** -- The `Confluent.SchemaRegistry.Serdes.Json` serializer works with plain C# classes annotated with `System.Text.Json` attributes.
- **Time-bound batch collection** -- The T3.x batch loop uses a `BatchTimeoutSeconds` deadline (default 5s) when collecting events for the `Task.WhenAll` window. This prevents indefinite waiting when fewer events arrive than the window size. In the test harness messages generate instantly so the timeout won't fire, but the code demonstrates the production pattern.
- **Concurrency window sizes 1, 10, and 100** -- Window-1 provides a sequential baseline within the batch pattern for direct comparison against higher concurrency levels. Window-50 was removed as it added little insight between 10 and 100.
- **Separate producer topics per record type** -- Avro SpecificRecord tests write to dedicated topics (e.g., `test-avro-small-specificrecord`) to avoid cross-contamination of benchmark data.
- **Pre-registered schemas** -- All schemas are registered ahead of time. Serializers use `AutoRegisterSchemas = false` and `UseLatestVersion = true` to look up schemas by subject at runtime.
- **Integer keys** -- All messages use sequential integer keys (1, 2, 3, ...) serialized with `AvroSerializer<int>` for Avro topics or `JsonSerializer<T>` / default `Serializers.Int32` for JSON topics.
- **Per-message uniqueness** -- Each message gets a unique `__test_seq` (sequence number) and `__test_ts` (ISO timestamp) stamped into the value before every produce call. This proves the serializer is doing real work on every message. One record template is created and mutated in-place to avoid object construction overhead.
- **Hybrid termination** -- Each test run stops when either the message count limit (default 100K) or the time limit (default 1 minute) is reached, whichever comes first. Set `DurationMinutes` to `null` for count-only mode.
- **Manual offset commit** -- All consumer tests use `EnableAutoCommit = false` to simulate real-world patterns where commit happens only after downstream processing confirms success. Two strategies are tested: ManualPerMessage (`consumer.Commit(result)` after every message) provides the strictest guarantee, while ManualBatch (`consumer.Commit()` every N messages) reduces broker round-trips for higher throughput. The batch size is configurable via `CommitBatchSize` (default 100).
- **Unique consumer group per run** -- Each consumer run creates a group ID like `throughput-test-T4.1-run-1-<guid>` so every run reads the full topic from the beginning. Note: The API key used must have permissions to dynamically register new consumer groups in Confluent Cloud.
- **Interactive HTML report** -- Every test run generates a self-contained HTML file using Chart.js (loaded from CDN). Bar charts compare throughput across all tests at a glance. When a time limit is configured, time-series line charts plot instantaneous throughput over the full run, making it easy to spot warm-up periods, throttling, or throughput degradation.
