using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeatherImageGenerator.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddHttpClient();

        services.AddSingleton(sp =>
        {
            var cs = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                     ?? "UseDevelopmentStorage=true";
            return new BlobServiceClient(cs);
        });

        services.AddSingleton(sp =>
        {
            var cs = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                     ?? "UseDevelopmentStorage=true";
            return new QueueServiceClient(cs, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
        });

        services.AddSingleton<JobStatusStore>();
        services.AddSingleton<QueueEnqueuer>();
        services.AddSingleton<BuienradarClient>();
        services.AddSingleton<PexelsImageProvider>();
        services.AddSingleton<ImageAnnotator>();

        // Auto-create queues/containers in Azurite/local dev.
        services.AddHostedService<StorageBootstrapper>();
    })
    .Build();

host.Run();
