using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace WeatherImageGenerator.Services;

/// <summary>
/// Ensures required queues and containers exist.
/// This fixes Azurite 404 spam and prevents queue triggers from staying disabled.
/// </summary>
public sealed class StorageBootstrapper : IHostedService
{
    private readonly QueueServiceClient _queueService;
    private readonly BlobServiceClient _blobService;
    private readonly IConfiguration _config;

    public StorageBootstrapper(QueueServiceClient queueService, BlobServiceClient blobService, IConfiguration config)
    {
        _queueService = queueService;
        _blobService = blobService;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Queues
        foreach (var q in new[] { "image-start", "image-process", "image-start-poison", "image-process-poison" })
        {
            await _queueService.GetQueueClient(q).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        // Containers
        var output = _config["OUTPUT_BLOB_CONTAINER"] ?? "weather-images";
        var cache = _config["CACHE_BLOB_CONTAINER"] ?? "background-cache";

        await _blobService.GetBlobContainerClient(output).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await _blobService.GetBlobContainerClient(cache).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
