using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WeatherImageGenerator.Models;
using WeatherImageGenerator.Services;

namespace WeatherImageGenerator.Functions;

public sealed class StartImageJobFunction
{
    private readonly QueueEnqueuer _queues;
    private readonly JobStatusStore _status;

    public StartImageJobFunction(QueueEnqueuer queues, JobStatusStore status)
    {
        _queues = queues;
        _status = status;
    }

    [Function("StartImageJob")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "jobs/start")] HttpRequestData req,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var jobId = Guid.NewGuid().ToString("N");

        string? requestedBy = null;
        try
        {
            using var doc = await JsonDocument.ParseAsync(req.Body, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("requestedBy", out var rb) && rb.ValueKind == JsonValueKind.String)
                requestedBy = rb.GetString();
        }
        catch
        {
           
        }

        var message = new StartJobMessage
        {
            JobId = jobId,
            RequestedBy = requestedBy,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        // Make status visible immediately.
        await _status.InitializeQueuedAsync(jobId, ct);

        await _queues.EnqueueJsonAsync("image-start", JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }), ct);

        var res = req.CreateResponse(HttpStatusCode.Accepted);
        await res.WriteAsJsonAsync(new
        {
            jobId,
            statusUrl = $"/api/jobs/{jobId}/status",
            resultsUrl = $"/api/jobs/{jobId}/images"
        }, cancellationToken: ct);

        return res;
    }
}
