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

    public static async Task ExportAsync(TestSuite suite, string filePath, string mode,
        string? deliveryLogJsFilename = null)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var html = GenerateHtml(suite, mode, deliveryLogJsFilename);
        await File.WriteAllTextAsync(filePath, html);
        Console.WriteLine($"HTML report exported to: {filePath}");
    }

    private static string GenerateHtml(TestSuite suite, string mode,
        string? deliveryLogJsFilename = null)
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
        if (!string.IsNullOrEmpty(deliveryLogJsFilename))
            sb.AppendLine($"  <script src=\"{deliveryLogJsFilename}\"></script>");
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

        // ── Test Filter Dropdown ──
        sb.AppendLine("  <div class=\"test-filter\">");
        sb.AppendLine($"    <button class=\"test-filter-btn\" id=\"testFilterBtn\">Filter Tests ({testIds.Count}/{testIds.Count})</button>");
        sb.AppendLine("    <div class=\"test-filter-dropdown\" id=\"testFilterDropdown\">");
        sb.AppendLine("      <div class=\"test-filter-actions\">");
        sb.AppendLine("        <a id=\"testFilterAll\">All</a>");
        sb.AppendLine("        <a id=\"testFilterNone\">None</a>");
        sb.AppendLine("      </div>");
        for (var i = 0; i < testIds.Count; i++)
        {
            var avg = suite.GetAverageForTest(testIds[i]);
            if (avg == null) continue;
            var filterLabel = $"{avg.TestId} {avg.TestName.Replace(" (Avg)", "")}";
            sb.AppendLine($"      <div class=\"test-filter-item\">");
            sb.AppendLine($"        <input type=\"checkbox\" id=\"testFilter_{i}\" data-index=\"{i}\" checked>");
            sb.AppendLine($"        <label for=\"testFilter_{i}\">{Escape(filterLabel)}</label>");
            sb.AppendLine($"      </div>");
        }
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

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
            var fireForgetResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T1")).ToList();
            var requestResponseResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T2")).ToList();
            var batchResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T3")).ToList();
            var consumerResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T4")).ToList();

            sb.AppendLine("  <h2>Throughput Over Time</h2>");

            if (fireForgetResults.Count > 0)
            {
                sb.AppendLine("  <h3>Fire-and-Forget Producer Tests (T1.x)</h3>");
                sb.AppendLine("  <div class=\"chart-row\">");
                sb.AppendLine("    <div class=\"chart-container-wide\"><canvas id=\"fireForgetTimeSeriesChart\"></canvas></div>");
                sb.AppendLine("  </div>");
            }

            if (requestResponseResults.Count > 0)
            {
                sb.AppendLine("  <h3>Request-Response Producer Tests (T2.x)</h3>");
                sb.AppendLine("  <div class=\"chart-row\">");
                sb.AppendLine("    <div class=\"chart-container-wide\"><canvas id=\"requestResponseTimeSeriesChart\"></canvas></div>");
                sb.AppendLine("  </div>");
            }

            if (batchResults.Count > 0)
            {
                sb.AppendLine("  <h3>Batch Processing Producer Tests (T3.x)</h3>");
                sb.AppendLine("  <div class=\"chart-row\">");
                sb.AppendLine("    <div class=\"chart-container-wide\"><canvas id=\"batchTimeSeriesChart\"></canvas></div>");
                sb.AppendLine("  </div>");
            }

            if (consumerResults.Count > 0)
            {
                sb.AppendLine("  <h3>Consumer Tests (T4.x)</h3>");
                sb.AppendLine("  <div class=\"chart-row\">");
                sb.AppendLine("    <div class=\"chart-container-wide\"><canvas id=\"consumerTimeSeriesChart\"></canvas></div>");
                sb.AppendLine("  </div>");
            }
        }

        // ── Delivery Log Viewer Section ──
        AppendDeliveryLogViewer(sb);

        // ── JavaScript ──
        sb.AppendLine("  <script>");
        AppendBarChartScript(sb, suite, testIds);
        if (resultsWithSamples.Count > 0)
            AppendTimeSeriesScript(sb, resultsWithSamples);
        sb.AppendLine("  </script>");

        // ── Test Filter Script ──
        sb.AppendLine("  <script>");
        AppendTestFilterScript(sb, testIds);
        sb.AppendLine("  </script>");

        // ── Delivery Log Viewer Script ──
        sb.AppendLine("  <script>");
        AppendDeliveryLogScript(sb);
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
        // Delivery log viewer styles
        sb.AppendLine("    .log-controls { display: flex; flex-wrap: wrap; gap: 12px; align-items: center;");
        sb.AppendLine("                    margin-bottom: 16px; }");
        sb.AppendLine("    .log-btn { padding: 8px 16px; border: 1px solid #ccc; border-radius: 4px;");
        sb.AppendLine("               background: #fff; cursor: pointer; font-size: 13px; transition: all 0.2s; }");
        sb.AppendLine("    .log-btn:hover { background: #e8eaf6; }");
        sb.AppendLine("    .log-btn.active { background: #1a237e; color: #fff; border-color: #1a237e; }");
        sb.AppendLine("    .log-search { padding: 8px 12px; border: 1px solid #ccc; border-radius: 4px;");
        sb.AppendLine("                  font-size: 13px; min-width: 250px; }");
        sb.AppendLine("    .log-search:focus { outline: none; border-color: #1a237e; }");
        sb.AppendLine("    .log-status { font-size: 13px; color: #666; margin-left: auto; }");
        sb.AppendLine("    #logTable th { text-align: left; }");
        sb.AppendLine("    #logTable td { text-align: left; font-size: 12px; font-family: 'SF Mono', Monaco, monospace; }");
        sb.AppendLine("    #logTable tr.log-success td:first-child { color: #2e7d32; font-weight: 600; }");
        sb.AppendLine("    #logTable tr.log-error { background: #fff5f5; }");
        sb.AppendLine("    #logTable tr.log-error td:first-child { color: #c62828; font-weight: 600; }");
        sb.AppendLine("    .log-pagination { display: flex; gap: 4px; align-items: center; justify-content: center;");
        sb.AppendLine("                      margin-top: 16px; flex-wrap: wrap; }");
        sb.AppendLine("    .log-pagination button { padding: 6px 12px; border: 1px solid #ccc; border-radius: 4px;");
        sb.AppendLine("                             background: #fff; cursor: pointer; font-size: 13px; }");
        sb.AppendLine("    .log-pagination button:hover { background: #e8eaf6; }");
        sb.AppendLine("    .log-pagination button.active { background: #1a237e; color: #fff; border-color: #1a237e; }");
        sb.AppendLine("    .log-pagination button:disabled { opacity: 0.4; cursor: default; }");
        sb.AppendLine("    .log-empty { text-align: center; padding: 48px 16px; color: #999; font-size: 14px; }");
        // Test filter dropdown styles
        sb.AppendLine("    .test-filter { position: relative; display: inline-block; margin-bottom: 16px; }");
        sb.AppendLine("    .test-filter-btn { padding: 8px 16px; border: 1px solid #ccc; border-radius: 4px;");
        sb.AppendLine("                       background: #fff; cursor: pointer; font-size: 13px; transition: all 0.2s; }");
        sb.AppendLine("    .test-filter-btn:hover { background: #e8eaf6; }");
        sb.AppendLine("    .test-filter-dropdown { display: none; position: absolute; left: 0; top: 100%; margin-top: 4px;");
        sb.AppendLine("                            background: #fff; border: 1px solid #ccc; border-radius: 4px;");
        sb.AppendLine("                            box-shadow: 0 4px 12px rgba(0,0,0,0.15); z-index: 1000;");
        sb.AppendLine("                            min-width: 320px; max-height: 400px; overflow-y: auto; padding: 8px 0; }");
        sb.AppendLine("    .test-filter-dropdown.open { display: block; }");
        sb.AppendLine("    .test-filter-actions { display: flex; gap: 12px; padding: 4px 12px 8px; border-bottom: 1px solid #eee; margin-bottom: 4px; }");
        sb.AppendLine("    .test-filter-actions a { font-size: 13px; color: #1a237e; cursor: pointer; text-decoration: underline; }");
        sb.AppendLine("    .test-filter-actions a:hover { color: #0d47a1; }");
        sb.AppendLine("    .test-filter-item { display: flex; align-items: center; padding: 4px 12px; cursor: pointer; }");
        sb.AppendLine("    .test-filter-item:hover { background: #f0f4ff; }");
        sb.AppendLine("    .test-filter-item input { margin-right: 8px; cursor: pointer; }");
        sb.AppendLine("    .test-filter-item label { font-size: 13px; cursor: pointer; flex: 1; }");
        sb.AppendLine("  </style>");
    }

    private static void AppendSummaryTable(StringBuilder sb, TestSuite suite, List<string> testIds)
    {
        sb.AppendLine("  <h2>Results Summary</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr>");
        sb.AppendLine("      <th>Test</th><th>API</th><th>Commit</th><th>Window</th><th>RecordType</th><th>Messages</th><th>Elapsed</th><th>Msgs/sec</th>");
        sb.AppendLine("      <th>MB/sec</th><th>Avg Latency (ms)</th><th>Peak CPU %</th><th>Peak Mem (MB)</th><th>Errors</th>");
        sb.AppendLine("    </tr></thead>");
        sb.AppendLine("    <tbody>");

        foreach (var testId in testIds)
        {
            var avg = suite.GetAverageForTest(testId);
            if (avg == null) continue;

            sb.AppendLine("    <tr>");
            sb.AppendLine($"      <td>{Escape(avg.TestId)} {Escape(avg.TestName.Replace(" (Avg)", ""))}</td>");
            sb.AppendLine($"      <td>{Escape(avg.ProduceApi)}</td>");
            sb.AppendLine($"      <td>{Escape(avg.CommitStrategy)}</td>");
            sb.AppendLine($"      <td>{(avg.ConcurrencyWindow > 0 ? avg.ConcurrencyWindow.ToString() : "-")}</td>");
            sb.AppendLine($"      <td>{Escape(avg.RecordType)}</td>");
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

        // Store original bar chart data for filtering
        sb.AppendLine($@"
    window.barChartFullData = {{
      labels: {labelsJson},
      msgSecData: [{string.Join(",", msgSecData)}],
      mbSecData: [{string.Join(",", mbSecData.Select(v => v.ToString("F2")))}],
      colors: {colorsJson}
    }};");

        // Msgs/sec bar chart
        sb.AppendLine($@"
    window.msgSecChart = new Chart(document.getElementById('msgSecChart'), {{
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
    window.mbSecChart = new Chart(document.getElementById('mbSecChart'), {{
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
        var fireForgetResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T1")).ToList();
        var requestResponseResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T2")).ToList();
        var batchResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T3")).ToList();
        var consumerResults = resultsWithSamples.Where(r => r.TestId.StartsWith("T4")).ToList();

        if (fireForgetResults.Count > 0)
            AppendTimeSeriesChart(sb, "fireForgetTimeSeriesChart", "Fire-and-Forget Producer Throughput Over Time", fireForgetResults);

        if (requestResponseResults.Count > 0)
            AppendTimeSeriesChart(sb, "requestResponseTimeSeriesChart", "Request-Response Producer Throughput Over Time", requestResponseResults);

        if (batchResults.Count > 0)
            AppendTimeSeriesChart(sb, "batchTimeSeriesChart", "Batch Processing Producer Throughput Over Time", batchResults);

        if (consumerResults.Count > 0)
            AppendTimeSeriesChart(sb, "consumerTimeSeriesChart", "Consumer Throughput Over Time", consumerResults);
    }

    private static void AppendTimeSeriesChart(StringBuilder sb, string canvasId, string title, List<TestResult> results)
    {
        // Build datasets: compute instantaneous msgs/sec from cumulative samples
        var datasets = new StringBuilder();
        var datasetTestIds = new List<string>(); // Track which testId each dataset belongs to
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var samples = r.Samples;
            if (samples.Count < 2) continue;
            datasetTestIds.Add(r.TestId);

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
    window.{canvasId}Instance = new Chart(document.getElementById('{canvasId}'), {{
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

        // Emit dataset-to-testId mapping for this time-series chart
        var datasetTestIdsJson = JsonSerializer.Serialize(datasetTestIds);
        sb.AppendLine($"    window.{canvasId}TestIds = {datasetTestIdsJson};");
    }

    private static void AppendDeliveryLogViewer(StringBuilder sb)
    {
        sb.AppendLine("  <h2>Producer Delivery Logs</h2>");
        sb.AppendLine("  <div id=\"logViewer\">");
        sb.AppendLine("    <div class=\"log-controls\">");
        sb.AppendLine("      <button class=\"log-btn active\" data-filter=\"all\" id=\"btnAll\">All</button>");
        sb.AppendLine("      <button class=\"log-btn\" data-filter=\"SUCCESS\" id=\"btnSuccess\">Success</button>");
        sb.AppendLine("      <button class=\"log-btn\" data-filter=\"ERROR\" id=\"btnError\">Error</button>");
        sb.AppendLine("      <input type=\"text\" class=\"log-search\" id=\"logSearch\" placeholder=\"Search testId, testName, msgKey, errorCode...\">");
        sb.AppendLine("      <span class=\"log-status\" id=\"logStatus\"></span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <table id=\"logTable\">");
        sb.AppendLine("      <thead><tr>");
        sb.AppendLine("        <th>Level</th><th>TestId</th><th>TestName</th><th>Run</th><th>MsgKey</th>");
        sb.AppendLine("        <th>Partition</th><th>Offset</th><th>Timestamp</th><th>Status</th>");
        sb.AppendLine("        <th>ErrorCode</th><th>ErrorReason</th>");
        sb.AppendLine("      </tr></thead>");
        sb.AppendLine("      <tbody id=\"logBody\"></tbody>");
        sb.AppendLine("    </table>");
        sb.AppendLine("    <div class=\"log-pagination\" id=\"logPagination\"></div>");
        sb.AppendLine("  </div>");
    }

    private static void AppendDeliveryLogScript(StringBuilder sb)
    {
        sb.AppendLine(@"
    (function() {
      var logs = window.DELIVERY_LOGS || [];
      var PAGE_SIZE = 100;
      var currentFilter = 'all';
      var searchTerm = '';
      var currentPage = 1;

      var btnAll = document.getElementById('btnAll');
      var btnSuccess = document.getElementById('btnSuccess');
      var btnError = document.getElementById('btnError');
      var logSearch = document.getElementById('logSearch');
      var logBody = document.getElementById('logBody');
      var logStatus = document.getElementById('logStatus');
      var logPagination = document.getElementById('logPagination');

      if (!logs.length) {
        logBody.innerHTML = '<tr><td colspan=""11"" class=""log-empty"">No delivery log entries found. ' +
          'Delivery logs are generated for all producer tests (T1.x–T3.x).</td></tr>';
        updateCounts();
        return;
      }

      function updateCounts() {
        var successCount = logs.filter(function(e) { return e.level === 'SUCCESS'; }).length;
        var errorCount = logs.filter(function(e) { return e.level === 'ERROR'; }).length;
        btnAll.textContent = 'All (' + logs.length + ')';
        btnSuccess.textContent = 'Success (' + successCount + ')';
        btnError.textContent = 'Error (' + errorCount + ')';
      }

      function getFiltered() {
        return logs.filter(function(e) {
          if (currentFilter !== 'all' && e.level !== currentFilter) return false;
          if (searchTerm) {
            var s = searchTerm.toLowerCase();
            var fields = [e.testId, e.testName, String(e.msgKey), e.errorCode || '', e.errorReason || ''];
            var match = false;
            for (var i = 0; i < fields.length; i++) {
              if (fields[i].toLowerCase().indexOf(s) >= 0) { match = true; break; }
            }
            if (!match) return false;
          }
          return true;
        });
      }

      function render() {
        var filtered = getFiltered();
        var totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
        if (currentPage > totalPages) currentPage = totalPages;

        var start = (currentPage - 1) * PAGE_SIZE;
        var end = Math.min(start + PAGE_SIZE, filtered.length);
        var page = filtered.slice(start, end);

        var html = '';
        for (var i = 0; i < page.length; i++) {
          var e = page[i];
          var cls = e.level === 'ERROR' ? 'log-error' : 'log-success';
          html += '<tr class=""' + cls + '"">' +
            '<td>' + esc(e.level) + '</td>' +
            '<td>' + esc(e.testId) + '</td>' +
            '<td>' + esc(e.testName) + '</td>' +
            '<td>' + e.run + '</td>' +
            '<td>' + e.msgKey + '</td>' +
            '<td>' + e.partition + '</td>' +
            '<td>' + e.offset + '</td>' +
            '<td>' + esc(e.timestamp) + '</td>' +
            '<td>' + esc(e.status) + '</td>' +
            '<td>' + esc(e.errorCode || '') + '</td>' +
            '<td>' + esc(e.errorReason || '') + '</td>' +
            '</tr>';
        }
        logBody.innerHTML = html || '<tr><td colspan=""11"" class=""log-empty"">No entries match the current filter.</td></tr>';

        // Status bar
        if (filtered.length > 0) {
          logStatus.textContent = 'Showing ' + (start + 1) + '-' + end + ' of ' + filtered.length + ' entries';
        } else {
          logStatus.textContent = '0 entries';
        }

        // Pagination
        var pagHtml = '';
        pagHtml += '<button ' + (currentPage <= 1 ? 'disabled' : '') + ' data-page=""' + (currentPage - 1) + '"">Prev</button>';

        var maxButtons = 7;
        var startPage = Math.max(1, currentPage - Math.floor(maxButtons / 2));
        var endPage = Math.min(totalPages, startPage + maxButtons - 1);
        if (endPage - startPage < maxButtons - 1) startPage = Math.max(1, endPage - maxButtons + 1);

        for (var p = startPage; p <= endPage; p++) {
          pagHtml += '<button data-page=""' + p + '""' + (p === currentPage ? ' class=""active""' : '') + '>' + p + '</button>';
        }

        pagHtml += '<button ' + (currentPage >= totalPages ? 'disabled' : '') + ' data-page=""' + (currentPage + 1) + '"">Next</button>';
        logPagination.innerHTML = pagHtml;
      }

      function esc(s) {
        if (!s) return '';
        return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
      }

      // Event: filter buttons
      document.querySelectorAll('.log-btn').forEach(function(btn) {
        btn.addEventListener('click', function() {
          document.querySelectorAll('.log-btn').forEach(function(b) { b.classList.remove('active'); });
          btn.classList.add('active');
          currentFilter = btn.getAttribute('data-filter');
          currentPage = 1;
          render();
        });
      });

      // Event: search (debounced)
      var debounceTimer;
      logSearch.addEventListener('input', function() {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(function() {
          searchTerm = logSearch.value;
          currentPage = 1;
          render();
        }, 300);
      });

      // Event: pagination (delegated)
      logPagination.addEventListener('click', function(ev) {
        var btn = ev.target.closest('button');
        if (!btn || btn.disabled) return;
        currentPage = parseInt(btn.getAttribute('data-page'), 10);
        render();
      });

      updateCounts();
      render();
    })();");
    }

    private static void AppendTestFilterScript(StringBuilder sb, List<string> testIds)
    {
        var testIdsJson = JsonSerializer.Serialize(testIds);
        sb.AppendLine($@"
    (function() {{
      var testIds = {testIdsJson};
      var total = testIds.length;
      var btn = document.getElementById('testFilterBtn');
      var dropdown = document.getElementById('testFilterDropdown');
      var checkboxes = dropdown.querySelectorAll('input[type=""checkbox""]');

      function getCheckedIndices() {{
        var indices = [];
        checkboxes.forEach(function(cb) {{
          if (cb.checked) indices.push(parseInt(cb.getAttribute('data-index'), 10));
        }});
        return indices;
      }}

      function updateButtonLabel() {{
        var count = getCheckedIndices().length;
        btn.textContent = 'Filter Tests (' + count + '/' + total + ')';
      }}

      function updateBarCharts(indices) {{
        var full = window.barChartFullData;
        if (!full) return;

        var filteredLabels = [];
        var filteredMsgSec = [];
        var filteredMbSec = [];
        var filteredColors = [];

        for (var i = 0; i < indices.length; i++) {{
          var idx = indices[i];
          filteredLabels.push(full.labels[idx]);
          filteredMsgSec.push(full.msgSecData[idx]);
          filteredMbSec.push(full.mbSecData[idx]);
          filteredColors.push(full.colors[idx]);
        }}

        if (window.msgSecChart) {{
          window.msgSecChart.data.labels = filteredLabels;
          window.msgSecChart.data.datasets[0].data = filteredMsgSec;
          window.msgSecChart.data.datasets[0].backgroundColor = filteredColors;
          window.msgSecChart.update();
        }}
        if (window.mbSecChart) {{
          window.mbSecChart.data.labels = filteredLabels;
          window.mbSecChart.data.datasets[0].data = filteredMbSec;
          window.mbSecChart.data.datasets[0].backgroundColor = filteredColors;
          window.mbSecChart.update();
        }}
      }}

      function updateTimeSeriesCharts(indices) {{
        var checkedTestIds = {{}};
        for (var i = 0; i < indices.length; i++) {{
          checkedTestIds[testIds[indices[i]]] = true;
        }}

        var charts = [
          {{ instance: window.fireForgetTimeSeriesChartInstance, testIds: window.fireForgetTimeSeriesChartTestIds }},
          {{ instance: window.requestResponseTimeSeriesChartInstance, testIds: window.requestResponseTimeSeriesChartTestIds }},
          {{ instance: window.batchTimeSeriesChartInstance, testIds: window.batchTimeSeriesChartTestIds }},
          {{ instance: window.consumerTimeSeriesChartInstance, testIds: window.consumerTimeSeriesChartTestIds }}
        ];

        for (var c = 0; c < charts.length; c++) {{
          var chart = charts[c].instance;
          var dsTestIds = charts[c].testIds;
          if (!chart || !dsTestIds) continue;
          for (var d = 0; d < dsTestIds.length; d++) {{
            var visible = !!checkedTestIds[dsTestIds[d]];
            chart.setDatasetVisibility(d, visible);
          }}
          chart.update();
        }}
      }}

      function applyFilter() {{
        var indices = getCheckedIndices();
        updateButtonLabel();
        updateBarCharts(indices);
        updateTimeSeriesCharts(indices);
      }}

      // Toggle dropdown
      btn.addEventListener('click', function(e) {{
        e.stopPropagation();
        dropdown.classList.toggle('open');
      }});

      // Close dropdown on outside click
      document.addEventListener('click', function(e) {{
        if (!dropdown.contains(e.target) && e.target !== btn) {{
          dropdown.classList.remove('open');
        }}
      }});

      // Checkbox change
      checkboxes.forEach(function(cb) {{
        cb.addEventListener('change', applyFilter);
      }});

      // All link
      document.getElementById('testFilterAll').addEventListener('click', function() {{
        checkboxes.forEach(function(cb) {{ cb.checked = true; }});
        applyFilter();
      }});

      // None link
      document.getElementById('testFilterNone').addEventListener('click', function() {{
        checkboxes.forEach(function(cb) {{ cb.checked = false; }});
        applyFilter();
      }});

      updateButtonLabel();
    }})();");
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
