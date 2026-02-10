namespace ConfluentThroughputTestHarness.Reporting;

public class DeliveryLogEntry
{
    public string Level { get; init; } = "";
    public string TestId { get; init; } = "";
    public string TestName { get; init; } = "";
    public int Run { get; init; }
    public int MsgKey { get; init; }
    public int Partition { get; init; }
    public long Offset { get; init; }
    public string Timestamp { get; init; } = "";
    public string Status { get; init; } = "";
    public string? ErrorCode { get; init; }
    public string? ErrorReason { get; init; }
}
