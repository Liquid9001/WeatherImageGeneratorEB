namespace WeatherImageGenerator.Models;

public sealed class ImageTaskMessage
{
    public required string JobId { get; init; }

    public int StationId { get; init; }
    public string StationName { get; init; } = "";
    public double TemperatureC { get; init; }
    public string Description { get; init; } = "";
}
