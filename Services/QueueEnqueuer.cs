using Azure.Storage.Queues;

namespace WeatherImageGenerator.Services;

public sealed class QueueEnqueuer
{
    private readonly QueueServiceClient _queueServiceClient;

    public QueueEnqueuer(QueueServiceClient queueServiceClient)
    {
        _queueServiceClient = queueServiceClient;
    }

    public async Task EnqueueJsonAsync(string queueName, string json, CancellationToken ct)
    {
        var queue = _queueServiceClient.GetQueueClient(queueName);
        await queue.CreateIfNotExistsAsync(cancellationToken: ct);
        await queue.SendMessageAsync(json, cancellationToken: ct);
    }
}
