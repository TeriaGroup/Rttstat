namespace Netpulse.Core.Models;

public sealed class Outage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public OutageCause Cause { get; set; }
    public Guid ProfileId { get; set; }
    public string? AdapterId { get; set; }
    public string Detail { get; set; } = "";

    public TimeSpan Duration => (EndedAtUtc ?? DateTimeOffset.UtcNow) - StartedAtUtc;
    public bool IsOpen => EndedAtUtc is null;
}

public sealed class AppEvent
{
    public long Ts { get; init; }
    public EventLevel Level { get; init; }
    public EventCategory Category { get; init; }
    public string Message { get; init; } = "";
}

public sealed class SpeedtestResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string ServerUrl { get; set; } = "";
    public string ServerName { get; set; } = "";
    public double? DownloadMbps { get; set; }
    public double? UploadMbps { get; set; }
    public double? PingMs { get; set; }
    public double? JitterMs { get; set; }
    public long BytesDown { get; set; }
    public long BytesUp { get; set; }
    public string? Error { get; set; }
    public Guid? ProfileId { get; set; }
    public string? AdapterId { get; set; }
}
