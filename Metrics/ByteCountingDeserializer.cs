// ────────────────────────────────────────────────────────────────────
// ByteCountingDeserializer.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Wraps an IAsyncDeserializer<T> to capture the raw byte
//           count of each consumed message before deserialization,
//           enabling accurate MB/sec metrics for consumer benchmarks.
// ────────────────────────────────────────────────────────────────────

using Confluent.Kafka;

namespace ConfluentThroughputTestHarness.Metrics;

/// <summary>
/// Decorator that wraps an IDeserializer&lt;T&gt; to accumulate the total
/// number of raw bytes seen before the inner deserializer processes them.
///
/// This is the synchronous variant -- used when the underlying deserializer
/// already implements IDeserializer (e.g., built-in Deserializers.Int32).
/// The consumer benchmark reads TotalBytes after consuming all messages
/// to compute MB/sec throughput.
///
/// Thread safety: uses Interlocked operations since the consumer's
/// deserialization may occur on different threads than the metric reader.
/// </summary>
public class ByteCountingDeserializer<T> : IDeserializer<T>
{
    private readonly IDeserializer<T> _inner;
    private long _totalBytes;

    public long TotalBytes => Interlocked.Read(ref _totalBytes);

    public ByteCountingDeserializer(IDeserializer<T> inner)
    {
        _inner = inner;
    }

    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        // Accumulate raw byte count BEFORE deserialization
        Interlocked.Add(ref _totalBytes, data.Length);
        return _inner.Deserialize(data, isNull, context);
    }

    public void Reset() => Interlocked.Exchange(ref _totalBytes, 0);
}

/// <summary>
/// Decorator that wraps an IAsyncDeserializer&lt;T&gt; to accumulate the total
/// number of raw bytes seen before the inner deserializer processes them.
///
/// This is the async variant -- used with Schema Registry deserializers
/// (AvroDeserializer, JsonDeserializer) which implement IAsyncDeserializer
/// because they may need to fetch schemas from SR on first use.
///
/// In the consumer benchmark, this wrapper is created around the SR deserializer
/// and then converted to synchronous via .AsSyncOverAsync() before being
/// passed to the ConsumerBuilder. After the consume loop completes, the
/// runner reads TotalBytes to compute MB/sec.
/// </summary>
public class ByteCountingAsyncDeserializer<T> : IAsyncDeserializer<T>
{
    private readonly IAsyncDeserializer<T> _inner;
    private long _totalBytes;

    public long TotalBytes => Interlocked.Read(ref _totalBytes);

    public ByteCountingAsyncDeserializer(IAsyncDeserializer<T> inner)
    {
        _inner = inner;
    }

    public async Task<T> DeserializeAsync(ReadOnlyMemory<byte> data, bool isNull, SerializationContext context)
    {
        // Accumulate raw byte count BEFORE deserialization
        Interlocked.Add(ref _totalBytes, data.Length);
        return await _inner.DeserializeAsync(data, isNull, context);
    }

    public void Reset() => Interlocked.Exchange(ref _totalBytes, 0);
}
