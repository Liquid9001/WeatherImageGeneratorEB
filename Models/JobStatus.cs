namespace WeatherImageGenerator.Models;

public sealed class JobStatus
{
    public string JobId { get; init; } = "";

    public string State { get; set; } = "queued"; // queued | running | completed | failed
    public string? Error { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public int Total { get; set; }
    public int Done { get; set; }

    public bool Completed => State is "completed";
    public double Percent => Total > 0 ? Math.Round((double)Done / Total * 100.0, 1) : 0.0;
}
