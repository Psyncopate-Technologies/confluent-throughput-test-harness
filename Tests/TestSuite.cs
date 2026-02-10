// ────────────────────────────────────────────────────────────────────
// TestSuite.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Aggregates individual TestResult instances and computes
//           averaged metrics per test ID for summary reporting.
// ────────────────────────────────────────────────────────────────────

namespace ConfluentThroughputTestHarness.Tests;

public class TestSuite
{
    public List<TestResult> Results { get; } = [];
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }

    public void AddResult(TestResult result) => Results.Add(result);

    public IEnumerable<TestResult> GetResultsForTest(string testId) =>
        Results.Where(r => r.TestId == testId);

    public TestResult? GetAverageForTest(string testId)
    {
        var runs = GetResultsForTest(testId).ToList();
        if (runs.Count == 0) return null;

        var totalElapsed = TimeSpan.FromMilliseconds(runs.Average(r => r.Elapsed.TotalMilliseconds));
        return new TestResult
        {
            TestId = testId,
            TestName = runs[0].TestName + " (Avg)",
            ProduceApi = runs[0].ProduceApi,
            CommitStrategy = runs[0].CommitStrategy,
            RecordType = runs[0].RecordType,
            ConcurrencyWindow = runs[0].ConcurrencyWindow,
            RunNumber = 0,
            MessageCount = (int)runs.Average(r => r.MessageCount),
            TotalBytes = (long)runs.Average(r => r.TotalBytes),
            Elapsed = totalElapsed,
            PeakCpuPercent = runs.Average(r => r.PeakCpuPercent),
            PeakMemoryBytes = (long)runs.Average(r => r.PeakMemoryBytes),
            DeliveryErrors = (int)runs.Average(r => r.DeliveryErrors)
        };
    }
}
