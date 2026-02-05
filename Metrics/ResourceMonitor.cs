// ────────────────────────────────────────────────────────────────────
// ResourceMonitor.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Background monitor that samples CPU usage and working set
//           memory every 250ms during a test run, tracking peak values
//           for benchmark reporting.
// ────────────────────────────────────────────────────────────────────

using System.Diagnostics;

namespace ConfluentThroughputTestHarness.Metrics;

public class ResourceMonitor : IDisposable
{
    private readonly Process _process;
    private readonly Timer _timer;
    private readonly object _lock = new();
    private double _peakCpuPercent;
    private long _peakMemoryBytes;
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;
    private bool _disposed;

    public double PeakCpuPercent { get { lock (_lock) return _peakCpuPercent; } }
    public long PeakMemoryBytes { get { lock (_lock) return _peakMemoryBytes; } }

    public ResourceMonitor(int sampleIntervalMs = 250)
    {
        _process = Process.GetCurrentProcess();
        _lastCpuTime = _process.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;
        _timer = new Timer(Sample, null, sampleIntervalMs, sampleIntervalMs);
    }

    private void Sample(object? state)
    {
        try
        {
            _process.Refresh();
            var now = DateTime.UtcNow;
            var currentCpuTime = _process.TotalProcessorTime;

            var cpuElapsed = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            var wallElapsed = (now - _lastSampleTime).TotalMilliseconds;

            if (wallElapsed > 0)
            {
                var cpuPercent = cpuElapsed / wallElapsed / Environment.ProcessorCount * 100.0;
                var memoryBytes = _process.WorkingSet64;

                lock (_lock)
                {
                    if (cpuPercent > _peakCpuPercent) _peakCpuPercent = cpuPercent;
                    if (memoryBytes > _peakMemoryBytes) _peakMemoryBytes = memoryBytes;
                }
            }

            _lastCpuTime = currentCpuTime;
            _lastSampleTime = now;
        }
        catch
        {
            // Ignore sampling errors
        }
    }

    public void Reset()
    {
        _process.Refresh();
        _lastCpuTime = _process.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;
        lock (_lock)
        {
            _peakCpuPercent = 0;
            _peakMemoryBytes = 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
        _process.Dispose();
    }
}
