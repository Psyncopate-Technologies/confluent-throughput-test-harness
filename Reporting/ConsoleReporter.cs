// ────────────────────────────────────────────────────────────────────
// ConsoleReporter.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Renders benchmark results as formatted Spectre.Console
//           tables, including per-run detail, per-test averages, and
//           a summary comparison across all tests.
// ────────────────────────────────────────────────────────────────────

using ConfluentThroughputTestHarness.Tests;
using Spectre.Console;

namespace ConfluentThroughputTestHarness.Reporting;

public static class ConsoleReporter
{
    public static void PrintResults(TestSuite suite)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold blue]Throughput Test Results[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // Detailed results table
        var detailTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Test[/]")
            .AddColumn("[bold]API[/]")
            .AddColumn("[bold]Commit[/]")
            .AddColumn("[bold]RecordType[/]")
            .AddColumn("[bold]Run[/]", c => c.RightAligned())
            .AddColumn("[bold]Messages[/]", c => c.RightAligned())
            .AddColumn("[bold]Elapsed[/]", c => c.RightAligned())
            .AddColumn("[bold]Msgs/sec[/]", c => c.RightAligned())
            .AddColumn("[bold]MB/sec[/]", c => c.RightAligned())
            .AddColumn("[bold]Avg Latency (ms)[/]", c => c.RightAligned())
            .AddColumn("[bold]Peak CPU %[/]", c => c.RightAligned())
            .AddColumn("[bold]Peak Mem (MB)[/]", c => c.RightAligned())
            .AddColumn("[bold]Errors[/]", c => c.RightAligned());

        var testIds = suite.Results.Select(r => r.TestId).Distinct().OrderBy(id => id);
        foreach (var testId in testIds)
        {
            var runs = suite.GetResultsForTest(testId).OrderBy(r => r.RunNumber);
            foreach (var r in runs)
            {
                detailTable.AddRow(
                    $"[yellow]{r.TestId}[/] {r.TestName}",
                    r.ProduceApi,
                    r.CommitStrategy,
                    r.RecordType,
                    r.RunNumber.ToString(),
                    r.MessageCount.ToString("N0"),
                    r.Elapsed.ToString(@"mm\:ss\.fff"),
                    r.MessagesPerSecond.ToString("N0"),
                    r.MegabytesPerSecond.ToString("F2"),
                    r.AvgLatencyMs.ToString("F4"),
                    r.PeakCpuPercent.ToString("F1"),
                    r.PeakMemoryMB.ToString("F1"),
                    r.DeliveryErrors > 0 ? $"[red]{r.DeliveryErrors}[/]" : "0"
                );
            }

            // Average row
            var avg = suite.GetAverageForTest(testId);
            if (avg != null)
            {
                detailTable.AddRow(
                    $"[bold green]{avg.TestId}[/] [bold]{avg.TestName}[/]",
                    $"[bold]{avg.ProduceApi}[/]",
                    $"[bold]{avg.CommitStrategy}[/]",
                    $"[bold]{avg.RecordType}[/]",
                    "[bold]AVG[/]",
                    $"[bold]{avg.MessageCount:N0}[/]",
                    $"[bold]{avg.Elapsed:mm\\:ss\\.fff}[/]",
                    $"[bold]{avg.MessagesPerSecond:N0}[/]",
                    $"[bold]{avg.MegabytesPerSecond:F2}[/]",
                    $"[bold]{avg.AvgLatencyMs:F4}[/]",
                    $"[bold]{avg.PeakCpuPercent:F1}[/]",
                    $"[bold]{avg.PeakMemoryMB:F1}[/]",
                    avg.DeliveryErrors > 0 ? $"[bold red]{avg.DeliveryErrors}[/]" : "[bold]0[/]"
                );
                detailTable.AddEmptyRow();
            }
        }

        AnsiConsole.Write(detailTable);

        // Summary comparison table
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold blue]Summary Comparison (Averages)[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Test[/]")
            .AddColumn("[bold]API[/]")
            .AddColumn("[bold]Commit[/]")
            .AddColumn("[bold]RecordType[/]")
            .AddColumn("[bold]Msgs/sec[/]", c => c.RightAligned())
            .AddColumn("[bold]MB/sec[/]", c => c.RightAligned())
            .AddColumn("[bold]Avg Latency (ms)[/]", c => c.RightAligned());

        foreach (var testId in testIds)
        {
            var avg = suite.GetAverageForTest(testId);
            if (avg != null)
            {
                summaryTable.AddRow(
                    $"{avg.TestId} {avg.TestName.Replace(" (Avg)", "")}",
                    avg.ProduceApi,
                    avg.CommitStrategy,
                    avg.RecordType,
                    avg.MessagesPerSecond.ToString("N0"),
                    avg.MegabytesPerSecond.ToString("F2"),
                    avg.AvgLatencyMs.ToString("F4")
                );
            }
        }

        AnsiConsole.Write(summaryTable);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]Test started: {suite.StartedAt:yyyy-MM-dd HH:mm:ss} | Completed: {suite.CompletedAt:yyyy-MM-dd HH:mm:ss} | Duration: {suite.CompletedAt - suite.StartedAt:hh\\:mm\\:ss}[/]");
    }
}
