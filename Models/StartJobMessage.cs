namespace WeatherImageGenerator.Models;

public sealed class StartJobMessage
{
    public required string JobId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? RequestedBy { get; init; }
}
