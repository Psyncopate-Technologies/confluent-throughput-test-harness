using Avro;
using Microsoft.Extensions.Configuration;
using ConfluentThroughputTestHarness.Config;
using ConfluentThroughputTestHarness.Reporting;
using ConfluentThroughputTestHarness.Runners;
using ConfluentThroughputTestHarness.Tests;
using Spectre.Console;

// ── Configuration ────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var kafkaSettings = new KafkaSettings();
config.GetSection("Kafka").Bind(kafkaSettings);

var srSettings = new SchemaRegistrySettings();
config.GetSection("SchemaRegistry").Bind(srSettings);

var testSettings = new TestSettings();
config.GetSection("Test").Bind(testSettings);

// ── CLI Arguments ────────────────────────────────────────────────────
var producerOnly = args.Contains("--producer-only", StringComparer.OrdinalIgnoreCase);
var consumerOnly = args.Contains("--consumer-only", StringComparer.OrdinalIgnoreCase);
var helpRequested = args.Contains("--help", StringComparer.OrdinalIgnoreCase);

string? specificTest = null;
var testIndex = Array.FindIndex(args, a => a.Equals("--test", StringComparison.OrdinalIgnoreCase));
if (testIndex >= 0 && testIndex + 1 < args.Length)
    specificTest = args[testIndex + 1];

if (helpRequested)
{
    PrintHelp();
    return;
}

// ── Load Schemas ─────────────────────────────────────────────────────
var schemasDir = Path.Combine(AppContext.BaseDirectory, "Schemas");
var smallSchemaJson = await File.ReadAllTextAsync(Path.Combine(schemasDir, "TestAvroDataTypesMsg.g.avsc"));
var largeSchemaJson = await File.ReadAllTextAsync(Path.Combine(schemasDir, "schema-cdc_freight_dbo_tblloads-value-v4.avsc"));

var smallSchema = (RecordSchema)Schema.Parse(smallSchemaJson);
var largeSchema = (RecordSchema)Schema.Parse(largeSchemaJson);

// ── Build Test Definitions ───────────────────────────────────────────
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
AnsiConsole.MarkupLine($"[grey]Messages per test:[/] {testSettings.MessageCount:N0}");
AnsiConsole.MarkupLine($"[grey]Tests to run:[/] {string.Join(", ", testList.Select(t => t.Id))}");
AnsiConsole.WriteLine();

// ── Execute Tests ────────────────────────────────────────────────────
var suite = new TestSuite { StartedAt = DateTime.UtcNow };
var producerRunner = new ProducerTestRunner(kafkaSettings, srSettings, smallSchema, largeSchema);
var consumerRunner = new ConsumerTestRunner(kafkaSettings, srSettings);

foreach (var test in testList)
{
    AnsiConsole.Write(new Rule($"[yellow]{test.Id}[/] {test.Name}").RuleStyle("grey").LeftJustified());

    for (var run = 1; run <= test.Runs; run++)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Run {run}/{test.Runs}...", async ctx =>
            {
                TestResult result;
                if (test.Type == TestType.Producer)
                    result = await producerRunner.RunAsync(test, run);
                else
                    result = await consumerRunner.RunAsync(test, run);

                suite.AddResult(result);

                AnsiConsole.MarkupLine(
                    $"  Run {run}: [green]{result.MessagesPerSecond:N0} msgs/sec[/] | " +
                    $"[cyan]{result.MegabytesPerSecond:F2} MB/sec[/] | " +
                    $"{result.Elapsed:mm\\:ss\\.fff} | " +
                    (result.DeliveryErrors > 0 ? $"[red]{result.DeliveryErrors} errors[/]" : "[grey]0 errors[/]"));
            });
    }

    AnsiConsole.WriteLine();
}

suite.CompletedAt = DateTime.UtcNow;

// ── Report Results ───────────────────────────────────────────────────
ConsoleReporter.PrintResults(suite);

var csvPath = Path.Combine(AppContext.BaseDirectory, "results",
    $"throughput-results-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
await CsvReporter.ExportAsync(suite, csvPath);

// ── Help ─────────────────────────────────────────────────────────────
static void PrintHelp()
{
    AnsiConsole.MarkupLine("[bold]Confluent Kafka Throughput Test Harness[/]");
    AnsiConsole.MarkupLine("[grey]Benchmarks Avro vs JSON serialization throughput with Confluent Cloud[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Usage:[/]");
    AnsiConsole.MarkupLine("  dotnet run                        Run all tests");
    AnsiConsole.MarkupLine("  dotnet run -- --producer-only     Run only producer tests (T1.x)");
    AnsiConsole.MarkupLine("  dotnet run -- --consumer-only     Run only consumer tests (T2.x)");
    AnsiConsole.MarkupLine("  dotnet run -- --test T1.1         Run a specific test");
    AnsiConsole.MarkupLine("  dotnet run -- --help              Show this help");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Tests:[/]");
    AnsiConsole.MarkupLine("  T1.1  Producer Avro Small (58 fields)");
    AnsiConsole.MarkupLine("  T1.2  Producer Avro Large (104 fields)");
    AnsiConsole.MarkupLine("  T1.3  Producer JSON Small (58 fields)");
    AnsiConsole.MarkupLine("  T1.4  Producer JSON Large (104 fields)");
    AnsiConsole.MarkupLine("  T2.1  Consumer Avro Small");
    AnsiConsole.MarkupLine("  T2.2  Consumer Avro Large");
    AnsiConsole.MarkupLine("  T2.3  Consumer JSON Small");
    AnsiConsole.MarkupLine("  T2.4  Consumer JSON Large");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Configuration:[/]");
    AnsiConsole.MarkupLine("  Edit appsettings.Development.json with your Confluent Cloud credentials");
}
