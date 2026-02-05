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

/// <summary>
/// Tracks peak CPU and memory usage during a benchmark run by sampling the
/// current process at a configurable interval (default 250ms).
///
/// Usage pattern:
///   using var monitor = new ResourceMonitor();
///   // ... run benchmark ...
///   var peakCpu = monitor.PeakCpuPercent;
///   var peakMem = monitor.PeakMemoryBytes;
///
/// The monitor starts sampling immediately on construction and stops when
/// disposed. Thread-safe: sampling runs on a Timer thread pool callback
/// while peak values are read from the benchmark thread.
/// </summary>
public class ResourceMonitor : IDisposable
{
    private readonly Process _process;
    private readonly Timer _timer;
    private readonly object _lock = new();

    // Peak values observed across all samples during the monitor's lifetime
    private double _peakCpuPercent;
    private long _peakMemoryBytes;

    // State for computing CPU percentage between consecutive samples
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;
    private bool _disposed;

    public double PeakCpuPercent { get { lock (_lock) return _peakCpuPercent; } }
    public long PeakMemoryBytes { get { lock (_lock) return _peakMemoryBytes; } }

    /// <param name="sampleIntervalMs">
    /// How often (in milliseconds) to sample CPU and memory. Default 250ms
    /// provides a good balance between accuracy and overhead.
    /// </param>
    public ResourceMonitor(int sampleIntervalMs = 250)
    {
        _process = Process.GetCurrentProcess();
        _lastCpuTime = _process.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;

        // Timer fires the Sample callback on a thread pool thread
        _timer = new Timer(Sample, null, sampleIntervalMs, sampleIntervalMs);
    }

    /// <summary>
    /// Timer callback that runs on a thread pool thread every sampleIntervalMs.
    /// Computes CPU% as (delta CPU time / delta wall time / core count * 100)
    /// and captures the process working set (physical memory).
    /// </summary>
    private void Sample(object? state)
    {
        try
        {
            _process.Refresh();   // Refresh cached process metrics from the OS
            var now = DateTime.UtcNow;
            var currentCpuTime = _process.TotalProcessorTime;

            // CPU% = (CPU time consumed in this interval) / (wall time elapsed) / (number of cores) * 100
            var cpuElapsed = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            var wallElapsed = (now - _lastSampleTime).TotalMilliseconds;

            if (wallElapsed > 0)
            {
                var cpuPercent = cpuElapsed / wallElapsed / Environment.ProcessorCount * 100.0;
                var memoryBytes = _process.WorkingSet64;   // Physical memory (RSS)

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
            // Ignore sampling errors -- they shouldn't affect benchmark results
        }
    }

    /// <summary>
    /// Resets peak values and CPU baseline. Useful if reusing a monitor across
    /// multiple benchmark phases (not currently used but available for future use).
    /// </summary>
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
