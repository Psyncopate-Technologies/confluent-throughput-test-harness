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
/// Wraps an IDeserializer to capture the raw byte count before deserialization.
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
        Interlocked.Add(ref _totalBytes, data.Length);
        return _inner.Deserialize(data, isNull, context);
    }

    public void Reset() => Interlocked.Exchange(ref _totalBytes, 0);
}

/// <summary>
/// Wraps an IAsyncDeserializer to capture the raw byte count before deserialization.
/// Used with Schema Registry deserializers which implement IAsyncDeserializer.
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
        Interlocked.Add(ref _totalBytes, data.Length);
        return await _inner.DeserializeAsync(data, isNull, context);
    }

    public void Reset() => Interlocked.Exchange(ref _totalBytes, 0);
}
