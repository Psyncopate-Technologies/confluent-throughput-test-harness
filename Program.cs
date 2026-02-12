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
//   --producer-only   Run only producer tests (T1.x–T3.x)
//   --consumer-only   Run only consumer tests (T4.x)
//   --test <ID>       Run a single test (e.g., T1.1 or T4.3)
//   --duration <N>    Run each test for N minutes (overrides MessageCount)
//   --refresh-schemas Force re-download of Avro schemas from Schema Registry
//   --help            Print usage information
var producerOnly = args.Contains("--producer-only", StringComparer.OrdinalIgnoreCase);
var consumerOnly = args.Contains("--consumer-only", StringComparer.OrdinalIgnoreCase);
var helpRequested = args.Contains("--help", StringComparer.OrdinalIgnoreCase);
var refreshSchemas = args.Contains("--refresh-schemas", StringComparer.OrdinalIgnoreCase);

string? specificTest = null;
var testIndex = Array.FindIndex(args, a => a.Equals("--test", StringComparison.OrdinalIgnoreCase));
if (testIndex >= 0 && testIndex + 1 < args.Length)
{
    // Collect all args after --test until the next flag (starts with --)
    var testArgs = args.Skip(testIndex + 1).TakeWhile(a => !a.StartsWith("--"));
    specificTest = string.Join(" ", testArgs).Trim();
}

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
// Download Avro value schemas from Schema Registry and cache them locally
// under schema-cache/. Subsequent runs read from the cache unless
// --refresh-schemas is specified. JSON schemas are not loaded here because
// the JSON serializer works with POCO classes and resolves schemas from
// Schema Registry at runtime.
var schemaCache = new SchemaRegistryCache(srSettings, forceRefresh: refreshSchemas);
var smallSchemaJson = await schemaCache.GetSchemaJsonAsync("test-avro-small-value");
var largeSchemaJson = await schemaCache.GetSchemaJsonAsync("test-avro-large-value");

var smallSchema = (RecordSchema)Schema.Parse(smallSchemaJson);
var largeSchema = (RecordSchema)Schema.Parse(largeSchemaJson);

// ── Build Test Definitions ───────────────────────────────────────────
// Generate the 24-test scenario-based matrix:
//   T1.1-T1.4   fire-and-forget producers (Produce + delivery handler)
//   T2.1-T2.4   request-response producers (ProduceAsync + await each)
//   T3.1-T3.12  batch processing producers (ProduceAsync + Task.WhenAll windows 1/10/100)
//   T4.1-T4.4   consumers
// then filter based on CLI arguments.
var allTests = TestDefinition.GetAll(testSettings);
var testsToRun = allTests.AsEnumerable();

if (specificTest != null)
{
    // Support range filters like "T1.1-T1.8"
    // Uses numeric comparison on the sub-number (e.g., 1 and 8 from T1.1-T1.8)
    // to avoid lexicographic issues where "T1.10" < "T1.8".
    if (specificTest.Contains('-'))
    {
        var parts = specificTest.Split('-', 2);
        var rangeStart = parts[0].Trim();
        var rangeEnd = parts[1].Trim();

        // Extract numeric ID for range bounds (e.g., "T1.8" → prefix "T1.", num 8)
        var startDot = rangeStart.LastIndexOf('.');
        var endDot = rangeEnd.LastIndexOf('.');
        var startPrefix = startDot >= 0 ? rangeStart[..(startDot + 1)] : "";
        var endPrefix = endDot >= 0 ? rangeEnd[..(endDot + 1)] : "";
        var startNum = startDot >= 0 && int.TryParse(rangeStart[(startDot + 1)..], out var sn) ? sn : -1;
        var endNum = endDot >= 0 && int.TryParse(rangeEnd[(endDot + 1)..], out var en) ? en : -1;

        testsToRun = testsToRun.Where(t =>
        {
            var dot = t.Id.LastIndexOf('.');
            if (dot < 0) return false;
            var prefix = t.Id[..(dot + 1)];
            if (!int.TryParse(t.Id[(dot + 1)..], out var num)) return false;
            return prefix.Equals(startPrefix, StringComparison.OrdinalIgnoreCase) &&
                   prefix.Equals(endPrefix, StringComparison.OrdinalIgnoreCase) &&
                   num >= startNum && num <= endNum;
        });
    }
    else if (specificTest.Contains(','))
    {
        // Comma-separated list: --test T1.1,T1.4,T1.8
        var ids = specificTest.Split(',').Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        testsToRun = testsToRun.Where(t => ids.Contains(t.Id));
    }
    else
    {
        testsToRun = testsToRun.Where(t => t.Id.Equals(specificTest, StringComparison.OrdinalIgnoreCase));
    }
}
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
    AnsiConsole.MarkupLine($"[grey]Mode:[/] Hybrid ({testSettings.MessageCount:N0} msgs or {testSettings.DurationMinutes} min, whichever first)");
else
    AnsiConsole.MarkupLine($"[grey]Mode:[/] Count-based ({testSettings.MessageCount:N0} messages per run)");
AnsiConsole.MarkupLine($"[grey]Tests to run:[/] {string.Join(", ", testList.Select(t => t.Id))}");
AnsiConsole.WriteLine();

// ── Execute Tests ────────────────────────────────────────────────────
// Create the test suite container and both runners. The ProducerTestRunner
// uses SpecificRecord (source-generated ISpecificRecord classes) for Avro;
// the ConsumerTestRunner discovers schemas from Schema Registry during deserialization.
var suite = new TestSuite { StartedAt = DateTime.UtcNow };
var deliveryLogger = new DeliveryLogger();
var producerRunner = new ProducerTestRunner(kafkaSettings, srSettings, testSettings, deliveryLogger);
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
var resultsDir = Path.Combine(AppContext.BaseDirectory, "results");
var csvPath = Path.Combine(resultsDir, $"throughput-results-{timestamp}.csv");
await CsvReporter.ExportAsync(suite, csvPath);

// Export delivery logs (JSONL for external tools, .js for HTML report)
var deliveryLogJsFilename = $"delivery-logs-{timestamp}.js";
if (deliveryLogger.Count > 0)
{
    var jsonlPath = Path.Combine(resultsDir, $"delivery-logs-{timestamp}.jsonl");
    var jsPath = Path.Combine(resultsDir, deliveryLogJsFilename);
    await deliveryLogger.WriteAsync(jsonlPath, jsPath);
}

// Generate interactive HTML chart report (especially useful for duration-mode runs
// where time-series throughput data is collected).
var mode = testSettings.DurationMinutes.HasValue ? "Duration" : "Count";
var htmlPath = Path.Combine(resultsDir, $"throughput-report-{timestamp}.html");
await HtmlChartReporter.ExportAsync(suite, htmlPath, mode, deliveryLogJsFilename);

// ── Help ─────────────────────────────────────────────────────────────
static void PrintHelp()
{
    AnsiConsole.MarkupLine("[bold]Confluent Kafka Throughput Test Harness[/]");
    AnsiConsole.MarkupLine("[grey]Benchmarks Avro vs JSON serialization throughput with Confluent Cloud[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Usage:[/]");
    AnsiConsole.MarkupLine("  dotnet run                        Run all 24 tests");
    AnsiConsole.MarkupLine("  dotnet run -- --duration 10       Run all tests for 10 minutes each");
    AnsiConsole.MarkupLine("  dotnet run -- --producer-only     Run only producer tests (T1.x–T3.x)");
    AnsiConsole.MarkupLine("  dotnet run -- --consumer-only     Run only consumer tests (T4.x)");
    AnsiConsole.MarkupLine("  dotnet run -- --test T1.1         Run a specific test");
    AnsiConsole.MarkupLine("  dotnet run -- --test T3.1-T3.12   Run a range of tests");
    AnsiConsole.MarkupLine("  dotnet run -- --test T1.1,T2.1    Run a comma-separated list of tests");
    AnsiConsole.MarkupLine("  dotnet run -- --refresh-schemas   Re-download schemas from Schema Registry");
    AnsiConsole.MarkupLine("  dotnet run -- --help              Show this help");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Modes:[/]");
    AnsiConsole.MarkupLine("  Hybrid (default):  Stop each run at 100K messages or 1 min, whichever comes first");
    AnsiConsole.MarkupLine("  Count-only:        Set DurationMinutes to null in appsettings.json");
    AnsiConsole.MarkupLine("  --duration N       Override the time limit to N minutes per run");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]All producer tests use acks=all + enable.idempotence=true[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Fire-and-Forget (T1.x):[/]");
    AnsiConsole.MarkupLine("  [grey]Produce + delivery handler callback[/]");
    AnsiConsole.MarkupLine("    T1.1  Avro Small    T1.2  Avro Large");
    AnsiConsole.MarkupLine("    T1.3  JSON Small    T1.4  JSON Large");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Request-Response (T2.x):[/]");
    AnsiConsole.MarkupLine("  [grey]ProduceAsync + await each message[/]");
    AnsiConsole.MarkupLine("    T2.1  Avro Small    T2.2  Avro Large");
    AnsiConsole.MarkupLine("    T2.3  JSON Small    T2.4  JSON Large");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Batch Processing (T3.x):[/]");
    AnsiConsole.MarkupLine("  [grey]ProduceAsync + Task.WhenAll concurrency windows[/]");
    AnsiConsole.MarkupLine("  [grey]Avro Small:[/]");
    AnsiConsole.MarkupLine("    T3.1  Window-1      T3.2  Window-10     T3.3  Window-100");
    AnsiConsole.MarkupLine("  [grey]Avro Large:[/]");
    AnsiConsole.MarkupLine("    T3.4  Window-1      T3.5  Window-10     T3.6  Window-100");
    AnsiConsole.MarkupLine("  [grey]JSON Small:[/]");
    AnsiConsole.MarkupLine("    T3.7  Window-1      T3.8  Window-10     T3.9  Window-100");
    AnsiConsole.MarkupLine("  [grey]JSON Large:[/]");
    AnsiConsole.MarkupLine("    T3.10 Window-1      T3.11 Window-10     T3.12 Window-100");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Consumer Tests (T4.x):[/]");
    AnsiConsole.MarkupLine("  T4.1  Consumer Avro Small");
    AnsiConsole.MarkupLine("  T4.2  Consumer Avro Large");
    AnsiConsole.MarkupLine("  T4.3  Consumer JSON Small");
    AnsiConsole.MarkupLine("  T4.4  Consumer JSON Large");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Configuration:[/]");
    AnsiConsole.MarkupLine("  Edit appsettings.Development.json with your Confluent Cloud credentials");
}
