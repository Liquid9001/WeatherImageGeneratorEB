using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WeatherImageGenerator.Services;

namespace WeatherImageGenerator.Functions;

public sealed class GetJobImagesFunction
{
    private readonly JobStatusStore _status;

    public GetJobImagesFunction(JobStatusStore status)
    {
        _status = status;
    }

    [Function("GetJobImages")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs/{jobId}/images")] HttpRequestData req,
        string jobId,
        CancellationToken ct)
    {
        var urls = await _status.ListImageUrlsAsync(jobId, ct);

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(new
        {
            jobId,
            count = urls.Count,
            images = urls
        }, cancellationToken: ct);

        return res;
    }
}
