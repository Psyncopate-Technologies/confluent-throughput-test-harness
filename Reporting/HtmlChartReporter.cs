// ────────────────────────────────────────────────────────────────────
// HtmlChartReporter.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Generates an interactive HTML report with Chart.js charts
//           including bar charts comparing throughput across tests and
//           time-series line charts showing throughput over time during
//           duration-mode runs.
// ────────────────────────────────────────────────────────────────────

using System.Text;
using System.Text.Json;
using ConfluentThroughputTestHarness.Tests;

namespace ConfluentThroughputTestHarness.Reporting;

public static class HtmlChartReporter
{
    // Consistent color palette: Avro Small, Avro Large, JSON Small, JSON Large
    private static readonly string[] Colors =
        ["#4CAF50", "#2196F3", "#FF9800", "#F44336", "#9C27B0", "#00BCD4", "#795548", "#607D8B"];

    private static readonly string[] ColorsAlpha =
        ["rgba(76,175,80,0.15)", "rgba(33,150,243,0.15)", "rgba(255,152,0,0.15)", "rgba(244,67,54,0.15)",
         "rgba(156,39,176,0.15)", "rgba(0,188,212,0.15)", "rgba(121,85,72,0.15)", "rgba(96,125,139,0.15)"];

    public static async Task ExportAsync(TestSuite suite, string filePath, string mode)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var html = GenerateHtml(suite, mode);
        await File.WriteAllTextAsync(filePath, html);
        Console.WriteLine($"HTML report exported to: {filePath}");
    }

    private static string GenerateHtml(TestSuite suite, string mode)
    {
        var testIds = suite.Results.Select(r => r.TestId).Distinct().OrderBy(id => id).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <title>Kafka Throughput Test Results</title>");
        sb.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/chart.js@4\"></script>");
        AppendStyles(sb);
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // ── Header ──
        sb.AppendLine("  <div class=\"header\">");
        sb.AppendLine("    <h1>Kafka Throughput Test Results</h1>");
        sb.AppendLine("    <p class=\"subtitle\">Avro vs JSON Serialization Benchmark</p>");
        sb.AppendLine("  </div>");

        // ── Metadata ──
        sb.AppendLine("  <div class=\"meta\">");
        sb.AppendLine($"    <span><strong>Mode:</strong> {mode}</span>");
        sb.AppendLine($"    <span><strong>Started:</strong> {suite.StartedAt:yyyy-MM-dd HH:mm:ss} UTC</span>");
        sb.AppendLine($"    <span><strong>Completed:</strong> {suite.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC</span>");
        sb.AppendLine($"    <span><strong>Duration:</strong> {suite.CompletedAt - suite.StartedAt:hh\\:mm\\:ss}</span>");
        sb.AppendLine("  </div>");

        // ── Summary Table ──
        AppendSummaryTable(sb, suite, testIds);

        // ── Bar Charts: Msgs/sec and MB/sec comparison ──
        sb.AppendLine("  <h2>Throughput Comparison</h2>");
        sb.AppendLine("  <div class=\"chart-row\">");
        sb.AppendLine("    <div class=\"chart-container\"><canvas id=\"msgSecChart\"></canvas></div>");
        sb.AppendLine("    <div class=\"chart-container\"><canvas id=\"mbSecChart\"></canvas></div>");
        sb.AppendLine("  </div>");

        // ── Time-series line charts (only if samples exist) ──
        var resultsWithSamples = suite.Results.Where(r => r.Samples.Count > 1).ToList();
        if (resultsWithSamples.Count > 0)
        {
            var producerResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T1")).ToList();
            var consumerResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T2")).ToList();

            sb.AppendLine("  <h2>Throughput Over Time</h2>");

            if (producerResults.Count > 0)
            {
                sb.AppendLine("  <h3>Producer Tests</h3>");
                sb.AppendLine("  <div class=\"chart-row\">");
                sb.AppendLine("    <div class=\"chart-container-wide\"><canvas id=\"producerTimeSeriesChart\"></canvas></div>");
                sb.AppendLine("  </div>");
            }

            if (consumerResults.Count > 0)
            {
                sb.AppendLine("  <h3>Consumer Tests</h3>");
                sb.AppendLine("  <div class=\"chart-row\">");
                sb.AppendLine("    <div class=\"chart-container-wide\"><canvas id=\"consumerTimeSeriesChart\"></canvas></div>");
                sb.AppendLine("  </div>");
            }
        }

        // ── JavaScript ──
        sb.AppendLine("  <script>");
        AppendBarChartScript(sb, suite, testIds);
        if (resultsWithSamples.Count > 0)
            AppendTimeSeriesScript(sb, resultsWithSamples);
        sb.AppendLine("  </script>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.AppendLine("  <style>");
        sb.AppendLine("    * { margin: 0; padding: 0; box-sizing: border-box; }");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;");
        sb.AppendLine("           background: #f5f5f5; color: #333; padding: 24px; max-width: 1400px; margin: 0 auto; }");
        sb.AppendLine("    .header { text-align: center; margin-bottom: 24px; }");
        sb.AppendLine("    .header h1 { font-size: 28px; color: #1a237e; }");
        sb.AppendLine("    .subtitle { color: #666; font-size: 16px; margin-top: 4px; }");
        sb.AppendLine("    .meta { display: flex; flex-wrap: wrap; gap: 24px; justify-content: center;");
        sb.AppendLine("            background: #fff; padding: 16px; border-radius: 8px; margin-bottom: 24px;");
        sb.AppendLine("            box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
        sb.AppendLine("    .meta span { font-size: 14px; }");
        sb.AppendLine("    h2 { font-size: 20px; color: #1a237e; margin: 32px 0 16px; border-bottom: 2px solid #1a237e; padding-bottom: 8px; }");
        sb.AppendLine("    h3 { font-size: 16px; color: #333; margin: 16px 0 8px; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; background: #fff; border-radius: 8px;");
        sb.AppendLine("            overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); margin-bottom: 24px; }");
        sb.AppendLine("    th { background: #1a237e; color: #fff; padding: 12px 16px; text-align: right; font-size: 13px; }");
        sb.AppendLine("    th:first-child { text-align: left; }");
        sb.AppendLine("    td { padding: 10px 16px; text-align: right; font-size: 13px; border-bottom: 1px solid #eee; }");
        sb.AppendLine("    td:first-child { text-align: left; font-weight: 500; }");
        sb.AppendLine("    tr:hover { background: #f0f4ff; }");
        sb.AppendLine("    .chart-row { display: flex; flex-wrap: wrap; gap: 24px; margin-bottom: 24px; }");
        sb.AppendLine("    .chart-container { flex: 1; min-width: 400px; background: #fff; padding: 16px;");
        sb.AppendLine("                       border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
        sb.AppendLine("    .chart-container-wide { width: 100%; background: #fff; padding: 16px;");
        sb.AppendLine("                            border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
        sb.AppendLine("  </style>");
    }

    private static void AppendSummaryTable(StringBuilder sb, TestSuite suite, List<string> testIds)
    {
        sb.AppendLine("  <h2>Results Summary</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr>");
        sb.AppendLine("      <th>Test</th><th>Messages</th><th>Elapsed</th><th>Msgs/sec</th>");
        sb.AppendLine("      <th>MB/sec</th><th>Avg Latency (ms)</th><th>Peak CPU %</th><th>Peak Mem (MB)</th><th>Errors</th>");
        sb.AppendLine("    </tr></thead>");
        sb.AppendLine("    <tbody>");

        foreach (var testId in testIds)
        {
            var avg = suite.GetAverageForTest(testId);
            if (avg == null) continue;

            sb.AppendLine("    <tr>");
            sb.AppendLine($"      <td>{Escape(avg.TestId)} {Escape(avg.TestName.Replace(" (Avg)", ""))}</td>");
            sb.AppendLine($"      <td>{avg.MessageCount:N0}</td>");
            sb.AppendLine($"      <td>{avg.Elapsed:mm\\:ss\\.fff}</td>");
            sb.AppendLine($"      <td><strong>{avg.MessagesPerSecond:N0}</strong></td>");
            sb.AppendLine($"      <td><strong>{avg.MegabytesPerSecond:F2}</strong></td>");
            sb.AppendLine($"      <td>{avg.AvgLatencyMs:F4}</td>");
            sb.AppendLine($"      <td>{avg.PeakCpuPercent:F1}</td>");
            sb.AppendLine($"      <td>{avg.PeakMemoryMB:F1}</td>");
            sb.AppendLine($"      <td>{avg.DeliveryErrors}</td>");
            sb.AppendLine("    </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
    }

    private static void AppendBarChartScript(StringBuilder sb, TestSuite suite, List<string> testIds)
    {
        var labels = new List<string>();
        var msgSecData = new List<double>();
        var mbSecData = new List<double>();
        var colorList = new List<string>();

        for (var i = 0; i < testIds.Count; i++)
        {
            var avg = suite.GetAverageForTest(testIds[i]);
            if (avg == null) continue;

            labels.Add($"{avg.TestId} {avg.TestName.Replace(" (Avg)", "")}");
            msgSecData.Add(Math.Round(avg.MessagesPerSecond, 0));
            mbSecData.Add(Math.Round(avg.MegabytesPerSecond, 2));
            colorList.Add(Colors[i % Colors.Length]);
        }

        var labelsJson = JsonSerializer.Serialize(labels);
        var colorsJson = JsonSerializer.Serialize(colorList);

        // Msgs/sec bar chart
        sb.AppendLine($@"
    new Chart(document.getElementById('msgSecChart'), {{
      type: 'bar',
      data: {{
        labels: {labelsJson},
        datasets: [{{
          label: 'Messages/sec',
          data: [{string.Join(",", msgSecData)}],
          backgroundColor: {colorsJson},
          borderWidth: 0,
          borderRadius: 4
        }}]
      }},
      options: {{
        responsive: true,
        plugins: {{
          title: {{ display: true, text: 'Messages per Second', font: {{ size: 16 }} }},
          legend: {{ display: false }}
        }},
        scales: {{
          y: {{ beginAtZero: true, title: {{ display: true, text: 'msgs/sec' }} }}
        }}
      }}
    }});");

        // MB/sec bar chart
        sb.AppendLine($@"
    new Chart(document.getElementById('mbSecChart'), {{
      type: 'bar',
      data: {{
        labels: {labelsJson},
        datasets: [{{
          label: 'MB/sec',
          data: [{string.Join(",", mbSecData.Select(v => v.ToString("F2")))}],
          backgroundColor: {colorsJson},
          borderWidth: 0,
          borderRadius: 4
        }}]
      }},
      options: {{
        responsive: true,
        plugins: {{
          title: {{ display: true, text: 'Megabytes per Second', font: {{ size: 16 }} }},
          legend: {{ display: false }}
        }},
        scales: {{
          y: {{ beginAtZero: true, title: {{ display: true, text: 'MB/sec' }} }}
        }}
      }}
    }});");
    }

    private static void AppendTimeSeriesScript(StringBuilder sb, List<TestResult> resultsWithSamples)
    {
        var producerResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T1")).ToList();
        var consumerResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T2")).ToList();

        if (producerResults.Count > 0)
            AppendTimeSeriesChart(sb, "producerTimeSeriesChart", "Producer Throughput Over Time", producerResults);

        if (consumerResults.Count > 0)
            AppendTimeSeriesChart(sb, "consumerTimeSeriesChart", "Consumer Throughput Over Time", consumerResults);
    }

    private static void AppendTimeSeriesChart(StringBuilder sb, string canvasId, string title, List<TestResult> results)
    {
        // Build datasets: compute instantaneous msgs/sec from cumulative samples
        var datasets = new StringBuilder();
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var samples = r.Samples;
            if (samples.Count < 2) continue;

            // Compute per-second throughput rate between consecutive samples
            var timePoints = new List<string>();
            var rates = new List<string>();

            for (var j = 1; j < samples.Count; j++)
            {
                var dt = samples[j].ElapsedSeconds - samples[j - 1].ElapsedSeconds;
                var dm = samples[j].CumulativeMessages - samples[j - 1].CumulativeMessages;
                var rate = dt > 0 ? dm / dt : 0;
                timePoints.Add(samples[j].ElapsedSeconds.ToString("F0"));
                rates.Add(rate.ToString("F0"));
            }

            var color = Colors[i % Colors.Length];
            var fillColor = ColorsAlpha[i % ColorsAlpha.Length];
            var label = $"{r.TestId} {r.TestName}";
            if (r.RunNumber > 0) label += $" (Run {r.RunNumber})";

            if (datasets.Length > 0) datasets.Append(",");
            datasets.Append($@"
        {{
          label: {JsonSerializer.Serialize(label)},
          data: [{string.Join(",", rates)}],
          borderColor: '{color}',
          backgroundColor: '{fillColor}',
          borderWidth: 2,
          pointRadius: 0,
          tension: 0.3,
          fill: true
        }}");
        }

        // Use the longest sample set for time labels
        var longestSamples = results
            .Where(r => r.Samples.Count > 1)
            .OrderByDescending(r => r.Samples.Count)
            .First().Samples;

        var labelsBuilder = new List<string>();
        for (var j = 1; j < longestSamples.Count; j++)
        {
            var secs = (int)longestSamples[j].ElapsedSeconds;
            var mins = secs / 60;
            var remSecs = secs % 60;
            labelsBuilder.Add($"\"{mins}:{remSecs:D2}\"");
        }

        sb.AppendLine($@"
    new Chart(document.getElementById('{canvasId}'), {{
      type: 'line',
      data: {{
        labels: [{string.Join(",", labelsBuilder)}],
        datasets: [{datasets}]
      }},
      options: {{
        responsive: true,
        interaction: {{ mode: 'index', intersect: false }},
        plugins: {{
          title: {{ display: true, text: '{title}', font: {{ size: 16 }} }},
          tooltip: {{
            callbacks: {{
              label: function(ctx) {{
                return ctx.dataset.label + ': ' + Math.round(ctx.parsed.y).toLocaleString() + ' msgs/sec';
              }}
            }}
          }}
        }},
        scales: {{
          x: {{ title: {{ display: true, text: 'Elapsed Time (mm:ss)' }},
                ticks: {{ maxTicksLimit: 20 }} }},
          y: {{ beginAtZero: true, title: {{ display: true, text: 'msgs/sec' }},
                ticks: {{ callback: function(v) {{ return v.toLocaleString(); }} }} }}
        }}
      }}
    }});");
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
