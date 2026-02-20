namespace WeatherImageGenerator.Models;

public sealed class StationReading
{
    public string StationName { get; init; } = "";
    public double TemperatureC { get; init; }
    public string Condition { get; init; } = "";
}
