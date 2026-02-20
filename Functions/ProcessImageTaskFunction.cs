using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using WeatherImageGenerator.Models;
using WeatherImageGenerator.Services;
using SixLabors.ImageSharp.Processing;

namespace WeatherImageGenerator.Functions;

public sealed class ProcessImageTaskFunction
{
    private readonly PexelsImageProvider _images;
    private readonly ImageAnnotator _annotator;
    private readonly JobStatusStore _status;

    public ProcessImageTaskFunction(PexelsImageProvider images, ImageAnnotator annotator, JobStatusStore status)
    {
        _images = images;
        _annotator = annotator;
        _status = status;
    }

    [Function("ProcessImageTask")]
    public async Task Run(
        [QueueTrigger("image-process", Connection = "AzureWebJobsStorage")] string message,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var task = JsonSerializer.Deserialize<ImageTaskMessage>(message, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Invalid image task message.");

        try
        {
            // Pick a background based on condition.
            var query = string.IsNullOrWhiteSpace(task.Description) ? "weather" : task.Description;
            await using var bgStream = await _images.GetBackgroundImageAsync(query, ct);

            using var bg = await Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgba32>(bgStream, ct);
            bg.Mutate(x => x.Resize(new ResizeOptions{Size = new Size(1024, 768),Mode = ResizeMode.Crop}));

            var stationLine = $"Meetstation {task.StationName}";
            var tempLine = $"{task.TemperatureC:0.0} °C";
            var condLine = string.IsNullOrWhiteSpace(task.Description) ? "" : task.Description;

            using var annotated = _annotator.Annotate(bg, stationLine, tempLine, condLine);

            await using var outStream = new MemoryStream();
            await annotated.SaveAsync(outStream, new JpegEncoder { Quality = 90 }, ct);
            outStream.Position = 0;

            var safeName = MakeSafeFileName(task.StationName);
            await _status.UploadImageAsync(task.JobId, $"{safeName}.jpg", outStream, "image/jpeg", ct);

            await _status.IncrementDoneAsync(task.JobId, ct);
        }
        catch (Exception ex)
        {
            await _status.FailAsync(task.JobId, ex.Message, ct);
            throw;
        }
    }

    private static string MakeSafeFileName(string input)
    {
        input = input.Trim();
        if (string.IsNullOrWhiteSpace(input)) return "station";
        var chars = input.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        var s = new string(chars);
        while (s.Contains("__", StringComparison.Ordinal)) s = s.Replace("__", "_", StringComparison.Ordinal);
        return s.Trim('_');
    }
}
