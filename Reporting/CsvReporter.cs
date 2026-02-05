using ConfluentThroughputTestHarness.Tests;

namespace ConfluentThroughputTestHarness.Reporting;

public static class CsvReporter
{
    public static async Task ExportAsync(TestSuite suite, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var lines = new List<string>
        {
            "TestId,TestName,RunNumber,MessageCount,TotalBytes,ElapsedMs,MessagesPerSecond,MegabytesPerSecond,AvgLatencyMs,PeakCpuPercent,PeakMemoryMB,DeliveryErrors"
        };

        foreach (var r in suite.Results.OrderBy(r => r.TestId).ThenBy(r => r.RunNumber))
        {
            lines.Add(string.Join(",",
                r.TestId,
                $"\"{r.TestName}\"",
                r.RunNumber,
                r.MessageCount,
                r.TotalBytes,
                r.Elapsed.TotalMilliseconds.ToString("F2"),
                r.MessagesPerSecond.ToString("F2"),
                r.MegabytesPerSecond.ToString("F4"),
                r.AvgLatencyMs.ToString("F6"),
                r.PeakCpuPercent.ToString("F2"),
                r.PeakMemoryMB.ToString("F2"),
                r.DeliveryErrors));
        }

        // Add average rows
        var testIds = suite.Results.Select(r => r.TestId).Distinct().OrderBy(id => id);
        foreach (var testId in testIds)
        {
            var avg = suite.GetAverageForTest(testId);
            if (avg != null)
            {
                lines.Add(string.Join(",",
                    avg.TestId,
                    $"\"{avg.TestName}\"",
                    "AVG",
                    avg.MessageCount,
                    avg.TotalBytes,
                    avg.Elapsed.TotalMilliseconds.ToString("F2"),
                    avg.MessagesPerSecond.ToString("F2"),
                    avg.MegabytesPerSecond.ToString("F4"),
                    avg.AvgLatencyMs.ToString("F6"),
                    avg.PeakCpuPercent.ToString("F2"),
                    avg.PeakMemoryMB.ToString("F2"),
                    avg.DeliveryErrors));
            }
        }

        await File.WriteAllLinesAsync(filePath, lines);
        Console.WriteLine($"Results exported to: {filePath}");
    }
}
