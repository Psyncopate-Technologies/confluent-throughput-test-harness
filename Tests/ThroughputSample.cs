// ────────────────────────────────────────────────────────────────────
// ThroughputSample.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Captures a point-in-time throughput snapshot during a test
//           run. Collected approximately every second by the onProgress
//           callback, these samples power the time-series charts in the
//           HTML report.
// ────────────────────────────────────────────────────────────────────

namespace ConfluentThroughputTestHarness.Tests;

public class ThroughputSample
{
    /// <summary>Seconds elapsed since the start of the run.</summary>
    public double ElapsedSeconds { get; init; }

    /// <summary>Total messages produced or consumed up to this point.</summary>
    public int CumulativeMessages { get; init; }
}
