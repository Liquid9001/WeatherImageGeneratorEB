using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using WeatherImageGenerator.Models;
using WeatherImageGenerator.Services;

namespace WeatherImageGenerator.Functions;

public sealed class FanOutStationsFunction
{
    private readonly BuienradarClient _buienradar;
    private readonly QueueEnqueuer _queues;
    private readonly JobStatusStore _status;

    public FanOutStationsFunction(BuienradarClient buienradar, QueueEnqueuer queues, JobStatusStore status)
    {
        _buienradar = buienradar;
        _queues = queues;
        _status = status;
    }

    [Function("FanOutStations")]
    public async Task Run(
        [QueueTrigger("image-start", Connection = "AzureWebJobsStorage")] string message,
        FunctionContext ctx,
        CancellationToken ct)
    {
        StartJobMessage start;
        try
        {
            start = JsonSerializer.Deserialize<StartJobMessage>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Invalid start job message.");
        }
        catch (Exception ex)
        {
            // Can't even parse. Nothing to update.
            throw new InvalidOperationException("Could not parse image-start message.", ex);
        }

        try
        {
            var readings = await _buienradar.GetStationReadingsAsync(ct);

            // Cap to keep local dev snappy.
            var selected = readings.Take(20).ToList();

            await _status.SetTotalAsync(start.JobId, selected.Count, ct);

            foreach (var r in selected)
            {
                var task = new ImageTaskMessage
                {
                    JobId = start.JobId,
                    StationName = r.StationName,
                    TemperatureC = r.TemperatureC,
                    Description = r.Condition
                };

                await _queues.EnqueueJsonAsync("image-process", JsonSerializer.Serialize(task, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }), ct);
            }
        }
        catch (Exception ex)
        {
            await _status.FailAsync(start.JobId, ex.Message, ct);
            throw;
        }
    }
}
