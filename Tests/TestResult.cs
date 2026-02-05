// ────────────────────────────────────────────────────────────────────
// TestResult.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Data class capturing per-run benchmark metrics including
//           messages/sec, MB/sec, average latency, peak CPU, peak
//           memory, and delivery error count.
// ────────────────────────────────────────────────────────────────────

namespace ConfluentThroughputTestHarness.Tests;

public class TestResult
{
    public string TestId { get; init; } = string.Empty;
    public string TestName { get; init; } = string.Empty;
    public int RunNumber { get; init; }
    public int MessageCount { get; init; }
    public long TotalBytes { get; init; }
    public TimeSpan Elapsed { get; init; }
    public double MessagesPerSecond => MessageCount / Elapsed.TotalSeconds;
    public double MegabytesPerSecond => TotalBytes / 1_048_576.0 / Elapsed.TotalSeconds;
    public double AvgLatencyMs => Elapsed.TotalMilliseconds / MessageCount;
    public double PeakCpuPercent { get; init; }
    public long PeakMemoryBytes { get; init; }
    public double PeakMemoryMB => PeakMemoryBytes / 1_048_576.0;
    public int DeliveryErrors { get; init; }
}
