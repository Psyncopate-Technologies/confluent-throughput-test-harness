// ────────────────────────────────────────────────────────────────────
// Program.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Main entry point and orchestrator for the Kafka throughput
//           test harness. Loads configuration, parses CLI arguments,
//           registers custom Avro logical types, and executes producer
//           and consumer benchmark tests in sequence.
// ────────────────────────────────────────────────────────────────────

using Avro;
using Avro.Util;
using Microsoft.Extensions.Configuration;
using ConfluentThroughputTestHarness.Config;
using ConfluentThroughputTestHarness.LogicalTypes;
using ConfluentThroughputTestHarness.Reporting;
using ConfluentThroughputTestHarness.Runners;
using ConfluentThroughputTestHarness.Tests;
using Spectre.Console;

// ── Configuration ────────────────────────────────────────────────────
// Build a layered configuration: appsettings.json provides defaults,
// appsettings.Development.json (gitignored) supplies real Confluent
// Cloud credentials, and environment variables take highest priority.
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Bind each configuration section to its strongly-typed POCO class.
var kafkaSettings = new KafkaSettings();
config.GetSection("Kafka").Bind(kafkaSettings);

var srSettings = new SchemaRegistrySettings();
config.GetSection("SchemaRegistry").Bind(srSettings);

var testSettings = new TestSettings();
config.GetSection("Test").Bind(testSettings);

// ── CLI Arguments ────────────────────────────────────────────────────
// Supported flags:
//   --producer-only   Run only T1.x producer tests
//   --consumer-only   Run only T2.x consumer tests
//   --test <ID>       Run a single test (e.g., T1.1 or T2.3)
//   --duration <N>    Run each test for N minutes (overrides MessageCount)
//   --help            Print usage information
var producerOnly = args.Contains("--producer-only", StringComparer.OrdinalIgnoreCase);
var consumerOnly = args.Contains("--consumer-only", StringComparer.OrdinalIgnoreCase);
var helpRequested = args.Contains("--help", StringComparer.OrdinalIgnoreCase);

string? specificTest = null;
var testIndex = Array.FindIndex(args, a => a.Equals("--test", StringComparison.OrdinalIgnoreCase));
if (testIndex >= 0 && testIndex + 1 < args.Length)
    specificTest = args[testIndex + 1];

// --duration N overrides the message-count based termination with a time-based one.
// The value from CLI takes precedence over appsettings.json DurationMinutes.
var durationIndex = Array.FindIndex(args, a => a.Equals("--duration", StringComparison.OrdinalIgnoreCase));
if (durationIndex >= 0 && durationIndex + 1 < args.Length && int.TryParse(args[durationIndex + 1], out var durationMinutes))
    testSettings.DurationMinutes = durationMinutes;

if (helpRequested)
{
    PrintHelp();
    return;
}

// ── Register Custom Logical Types ────────────────────────────────────
// The freight CDC Avro schemas use custom logical types "varchar" and "char"
// that are not part of the standard Avro spec. These must be registered with
// the Avro library before parsing the schemas, otherwise Schema.Parse() will
// throw an unknown logical type error.
LogicalTypeFactory.Instance.Register(new VarcharLogicalType());
LogicalTypeFactory.Instance.Register(new CharLogicalType());

// ── Load Schemas ─────────────────────────────────────────────────────
// Read and parse the two Avro value schemas from the Schemas/ directory.
// These are used by ProducerTestRunner to build GenericRecord instances.
// JSON schemas are not loaded here because the JSON serializer works with
// POCO classes and resolves schemas from Schema Registry at runtime.
var schemasDir = Path.Combine(AppContext.BaseDirectory, "Schemas");
var smallSchemaJson = await File.ReadAllTextAsync(Path.Combine(schemasDir, "test-avro-small-value.avsc"));
var largeSchemaJson = await File.ReadAllTextAsync(Path.Combine(schemasDir, "test-avro-large-value.avsc"));

var smallSchema = (RecordSchema)Schema.Parse(smallSchemaJson);
var largeSchema = (RecordSchema)Schema.Parse(largeSchemaJson);

// ── Build Test Definitions ───────────────────────────────────────────
// Generate the full 8-test matrix (T1.1-T1.4 producers, T2.1-T2.4 consumers)
// from TestSettings, then filter based on CLI arguments.
var allTests = TestDefinition.GetAll(testSettings);
var testsToRun = allTests.AsEnumerable();

if (specificTest != null)
    testsToRun = testsToRun.Where(t => t.Id.Equals(specificTest, StringComparison.OrdinalIgnoreCase));
else if (producerOnly)
    testsToRun = testsToRun.Where(t => t.Type == TestType.Producer);
else if (consumerOnly)
    testsToRun = testsToRun.Where(t => t.Type == TestType.Consumer);

var testList = testsToRun.ToList();
if (testList.Count == 0)
{
    AnsiConsole.MarkupLine("[red]No tests matched the filter criteria.[/]");
    return;
}

// ── Print Banner ─────────────────────────────────────────────────────
AnsiConsole.Write(new FigletText("Kafka Throughput").Color(Color.Blue));
AnsiConsole.Write(new Rule("[bold]Avro vs JSON Serialization Benchmark[/]").RuleStyle("grey"));
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine($"[grey]Bootstrap:[/] {kafkaSettings.BootstrapServers}");
AnsiConsole.MarkupLine($"[grey]Schema Registry:[/] {srSettings.Url}");
if (testSettings.DurationMinutes.HasValue)
    AnsiConsole.MarkupLine($"[grey]Mode:[/] Duration-based ({testSettings.DurationMinutes} min per run)");
else
    AnsiConsole.MarkupLine($"[grey]Mode:[/] Count-based ({testSettings.MessageCount:N0} messages per run)");
AnsiConsole.MarkupLine($"[grey]Tests to run:[/] {string.Join(", ", testList.Select(t => t.Id))}");
AnsiConsole.WriteLine();

// ── Execute Tests ────────────────────────────────────────────────────
// Create the test suite container and both runners. The ProducerTestRunner
// needs the parsed Avro schemas to build GenericRecords; the ConsumerTestRunner
// does not because it discovers schemas from Schema Registry during deserialization.
var suite = new TestSuite { StartedAt = DateTime.UtcNow };
var producerRunner = new ProducerTestRunner(kafkaSettings, srSettings, testSettings, smallSchema, largeSchema);
var consumerRunner = new ConsumerTestRunner(kafkaSettings, srSettings);

// Iterate through each test, executing the configured number of runs.
// Duration mode shows a progress bar with countdown; count mode shows a spinner.
foreach (var test in testList)
{
    AnsiConsole.Write(new Rule($"[yellow]{test.Id}[/] {test.Name}").RuleStyle("grey").LeftJustified());

    for (var run = 1; run <= test.Runs; run++)
    {
        TestResult result;

        if (test.Duration.HasValue)
        {
            // ── Duration mode: show a spinner with live countdown in the status text ──
            // Also collect time-series throughput samples for HTML chart reporting.
            var totalSeconds = test.Duration.Value.TotalSeconds;
            var durationDisplay = test.Duration.Value.ToString(@"mm\:ss");
            TestResult? progressResult = null;
            var samples = new List<ThroughputSample>();

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(
                    $"Run {run}/{test.Runs} | 0 msgs | 00:00 / {durationDisplay} | {durationDisplay} remaining",
                    async ctx =>
                {
                    Action<int, TimeSpan> onProgress = (msgs, elapsed) =>
                    {
                        // Collect throughput sample for time-series charts
                        samples.Add(new ThroughputSample
                        {
                            ElapsedSeconds = elapsed.TotalSeconds,
                            CumulativeMessages = msgs
                        });

                        var remaining = test.Duration!.Value - elapsed;
                        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                        var pct = Math.Min(100, (int)(elapsed.TotalSeconds / totalSeconds * 100));
                        var filled = pct / 5;   // 20-char bar
                        var bar = new string('\u2588', filled) + new string('\u2591', 20 - filled);
                        ctx.Status(
                            $"Run {run}/{test.Runs} | {msgs:N0} msgs | " +
                            $"{elapsed:mm\\:ss} / {durationDisplay} | " +
                            $"{remaining:mm\\:ss} remaining | {bar} {pct}%");
                    };

                    if (test.Type == TestType.Producer)
                        progressResult = await producerRunner.RunAsync(test, run, onProgress);
                    else
                        progressResult = await consumerRunner.RunAsync(test, run, onProgress);
                });

            result = progressResult!;
            result.Samples = samples;
        }
        else
        {
            // ── Count mode: show a simple spinner ──
            TestResult? spinnerResult = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Run {run}/{test.Runs}...", async ctx =>
                {
                    if (test.Type == TestType.Producer)
                        spinnerResult = await producerRunner.RunAsync(test, run);
                    else
                        spinnerResult = await consumerRunner.RunAsync(test, run);
                });

            result = spinnerResult!;
        }

        suite.AddResult(result);

        // Print per-run summary: msgs/sec, MB/sec, elapsed time, message count, error count
        AnsiConsole.MarkupLine(
            $"  Run {run}: [green]{result.MessagesPerSecond:N0} msgs/sec[/] | " +
            $"[cyan]{result.MegabytesPerSecond:F2} MB/sec[/] | " +
            $"{result.Elapsed:mm\\:ss\\.fff} | " +
            $"[grey]{result.MessageCount:N0} msgs[/] | " +
            (result.DeliveryErrors > 0 ? $"[red]{result.DeliveryErrors} errors[/]" : "[grey]0 errors[/]"));
    }

    AnsiConsole.WriteLine();
}

suite.CompletedAt = DateTime.UtcNow;

// ── Report Results ───────────────────────────────────────────────────
// Print the detailed results table and summary comparison to the console,
// then export all results (including per-test averages) to a timestamped CSV.
ConsoleReporter.PrintResults(suite);

var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
var csvPath = Path.Combine(AppContext.BaseDirectory, "results",
    $"throughput-results-{timestamp}.csv");
await CsvReporter.ExportAsync(suite, csvPath);

// Generate interactive HTML chart report (especially useful for duration-mode runs
// where time-series throughput data is collected).
var mode = testSettings.DurationMinutes.HasValue ? "Duration" : "Count";
var htmlPath = Path.Combine(AppContext.BaseDirectory, "results",
    $"throughput-report-{timestamp}.html");
await HtmlChartReporter.ExportAsync(suite, htmlPath, mode);

// ── Help ─────────────────────────────────────────────────────────────
static void PrintHelp()
{
    AnsiConsole.MarkupLine("[bold]Confluent Kafka Throughput Test Harness[/]");
    AnsiConsole.MarkupLine("[grey]Benchmarks Avro vs JSON serialization throughput with Confluent Cloud[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Usage:[/]");
    AnsiConsole.MarkupLine("  dotnet run                        Run all tests (count-based, default 100K msgs)");
    AnsiConsole.MarkupLine("  dotnet run -- --duration 10       Run all tests for 10 minutes each");
    AnsiConsole.MarkupLine("  dotnet run -- --producer-only     Run only producer tests (T1.x)");
    AnsiConsole.MarkupLine("  dotnet run -- --consumer-only     Run only consumer tests (T2.x)");
    AnsiConsole.MarkupLine("  dotnet run -- --test T1.1         Run a specific test");
    AnsiConsole.MarkupLine("  dotnet run -- --help              Show this help");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Modes:[/]");
    AnsiConsole.MarkupLine("  Count-based (default):  Produce/consume a fixed number of messages per run");
    AnsiConsole.MarkupLine("  Duration-based:         Produce/consume for a fixed time (--duration N minutes)");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Tests:[/]");
    AnsiConsole.MarkupLine("  T1.1  Producer Avro Small (27 fields)");
    AnsiConsole.MarkupLine("  T1.2  Producer Avro Large (106 fields)");
    AnsiConsole.MarkupLine("  T1.3  Producer JSON Small (27 fields)");
    AnsiConsole.MarkupLine("  T1.4  Producer JSON Large (106 fields)");
    AnsiConsole.MarkupLine("  T2.1  Consumer Avro Small");
    AnsiConsole.MarkupLine("  T2.2  Consumer Avro Large");
    AnsiConsole.MarkupLine("  T2.3  Consumer JSON Small");
    AnsiConsole.MarkupLine("  T2.4  Consumer JSON Large");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Configuration:[/]");
    AnsiConsole.MarkupLine("  Edit appsettings.Development.json with your Confluent Cloud credentials");
}
