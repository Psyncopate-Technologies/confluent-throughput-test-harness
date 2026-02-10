using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConfluentThroughputTestHarness.Reporting;

public class DeliveryLogger
{
    private readonly ConcurrentQueue<DeliveryLogEntry> _entries = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public int Count => _entries.Count;

    public void LogSuccess(string testId, string testName, int run, int msgKey,
        int partition, long offset)
    {
        _entries.Enqueue(new DeliveryLogEntry
        {
            Level = "SUCCESS",
            TestId = testId,
            TestName = testName,
            Run = run,
            MsgKey = msgKey,
            Partition = partition,
            Offset = offset,
            Timestamp = DateTime.UtcNow.ToString("O"),
            Status = "Persisted"
        });
    }

    public void LogError(string testId, string testName, int run, int msgKey,
        int partition, long offset, string errorCode, string errorReason)
    {
        _entries.Enqueue(new DeliveryLogEntry
        {
            Level = "ERROR",
            TestId = testId,
            TestName = testName,
            Run = run,
            MsgKey = msgKey,
            Partition = partition,
            Offset = offset,
            Timestamp = DateTime.UtcNow.ToString("O"),
            Status = "NotPersisted",
            ErrorCode = errorCode,
            ErrorReason = errorReason
        });
    }

    public async Task WriteAsync(string jsonlPath, string jsPath)
    {
        var dir = Path.GetDirectoryName(jsonlPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var entries = _entries.ToArray();

        // Write JSONL file (one JSON object per line)
        var jsonlSb = new StringBuilder();
        foreach (var entry in entries)
            jsonlSb.AppendLine(JsonSerializer.Serialize(entry, JsonOptions));

        await File.WriteAllTextAsync(jsonlPath, jsonlSb.ToString());

        // Write companion .js file (window.DELIVERY_LOGS = [...])
        var jsSb = new StringBuilder();
        jsSb.Append("window.DELIVERY_LOGS = ");
        jsSb.Append(JsonSerializer.Serialize(entries, JsonOptions));
        jsSb.AppendLine(";");

        await File.WriteAllTextAsync(jsPath, jsSb.ToString());

        Console.WriteLine($"Delivery logs exported: {entries.Length} entries to {jsonlPath}");
    }
}
