using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WeatherImageGenerator.Services;

namespace WeatherImageGenerator.Functions;

public sealed class GetJobStatusFunction
{
    private readonly JobStatusStore _status;

    public GetJobStatusFunction(JobStatusStore status)
    {
        _status = status;
    }

    [Function("GetJobStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs/{jobId}/status")] HttpRequestData req,
        string jobId,
        CancellationToken ct)
    {
        var status = await _status.GetStatusAsync(jobId, ct);
        if (status is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new
        {
            jobId = status.JobId,
            state = status.State,
            error = status.Error,
            createdAtUtc = status.CreatedAtUtc,
            total = status.Total,
            done = status.Done,
            percent = status.Percent,
            completed = status.Completed,
            resultsUrl = $"/api/jobs/{jobId}/images"
        }, cancellationToken: ct);

        return res;
    }
}
