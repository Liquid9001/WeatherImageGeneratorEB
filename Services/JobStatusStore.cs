using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using WeatherImageGenerator.Models;

namespace WeatherImageGenerator.Services;

public sealed class JobStatusStore
{
    private readonly BlobContainerClient _container;
    private readonly bool _publicBlobAccess;

    public JobStatusStore(BlobServiceClient blobServiceClient, IConfiguration config)
    {
        var containerName = config["OUTPUT_BLOB_CONTAINER"] ?? "weather-images";
        _container = blobServiceClient.GetBlobContainerClient(containerName);
        _publicBlobAccess = bool.TryParse(config["PUBLIC_BLOB_ACCESS"], out var isPublic) && isPublic;
    }

    public async Task InitializeQueuedAsync(string jobId, CancellationToken ct)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);

        var status = new JobStatus
        {
            JobId = jobId,
            State = "queued",
            Total = 0,
            Done = 0,
            Error = null,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await WriteStatusAsync(status, overwrite: false, ct);
    }

    public async Task SetTotalAsync(string jobId, int total, CancellationToken ct)
    {
        var status = await GetStatusOrDefaultAsync(jobId, ct);
        status.Total = total;
        status.State = total > 0 ? "running" : "completed";
        await WriteStatusAsync(status, overwrite: true, ct);
    }

    public async Task IncrementDoneAsync(string jobId, CancellationToken ct)
    {
        var status = await GetStatusOrDefaultAsync(jobId, ct);
        status.Done = Math.Min(status.Total, status.Done + 1);

        if (status.Total > 0 && status.Done >= status.Total)
        {
            status.State = "completed";
        }
        else
        {
            status.State = "running";
        }

        await WriteStatusAsync(status, overwrite: true, ct);
    }

    public async Task FailAsync(string jobId, string error, CancellationToken ct)
    {
        var status = await GetStatusOrDefaultAsync(jobId, ct);
        status.State = "failed";
        status.Error = error;
        await WriteStatusAsync(status, overwrite: true, ct);
    }

    public async Task<JobStatus?> GetStatusAsync(string jobId, CancellationToken ct)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = _container.GetBlobClient(StatusPath(jobId));

        if (!await blob.ExistsAsync(ct))
            return null;

        var download = await blob.DownloadContentAsync(ct);
        return download.Value.Content.ToObjectFromJson<JobStatus>();
    }

    public async Task<IReadOnlyList<string>> ListImageUrlsAsync(string jobId, CancellationToken ct)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);

        var prefix = $"jobs/{jobId}/images/";
        var results = new List<string>();

        await foreach (var item in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            var blob = _container.GetBlobClient(item.Name);
            results.Add(GetReadableUrl(blob));
        }

        return results;
    }

    public async Task UploadImageAsync(string jobId, string fileName, Stream content, string contentType, CancellationToken ct)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = _container.GetBlobClient($"jobs/{jobId}/images/{fileName}");

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
    }

    private async Task<JobStatus> GetStatusOrDefaultAsync(string jobId, CancellationToken ct)
    {
        var existing = await GetStatusAsync(jobId, ct);
        return existing ?? new JobStatus { JobId = jobId, State = "queued", Total = 0, Done = 0, CreatedAtUtc = DateTimeOffset.UtcNow };
    }

    private async Task WriteStatusAsync(JobStatus status, bool overwrite, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(StatusPath(status.JobId));

        var json = JsonSerializer.Serialize(status, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        if (!overwrite)
        {
            try
            {
                await blob.UploadAsync(ms, new BlobUploadOptions
                {
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                }, ct);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // Already exists, fall through to overwrite
                ms.Position = 0;
            }
        }

        ms.Position = 0;
        await blob.UploadAsync(ms, overwrite: true, cancellationToken: ct);
    }

    private string GetReadableUrl(BlobClient blob)
    {
        if (_publicBlobAccess)
            return blob.Uri.ToString();

        if (!blob.CanGenerateSasUri)
            return blob.Uri.ToString();

        var sas = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blob.Name,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(4)
        };

        sas.SetPermissions(BlobSasPermissions.Read);
        return blob.GenerateSasUri(sas).ToString();
    }

    private static string StatusPath(string jobId) => $"jobs/{jobId}/status.json";
}
