using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace WeatherImageGenerator.Services;

public sealed class PexelsImageProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BlobContainerClient _cacheContainer;
    private readonly string? _apiKey;
    private readonly string _fallbackUrl;

    public PexelsImageProvider(
        IHttpClientFactory httpClientFactory,
        BlobServiceClient blobServiceClient,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;

        var cacheContainerName = configuration["CACHE_BLOB_CONTAINER"] ?? "background-cache";
        _cacheContainer = blobServiceClient.GetBlobContainerClient(cacheContainerName);

        _apiKey = configuration["PEXELS_API_KEY"];

        // Public, no-key fallback image source.
        // Using a seed keeps it stable per station/condition.
        _fallbackUrl = configuration["FALLBACK_IMAGE_URL"] ?? "https://picsum.photos/seed/{seed}/1024/768";
    }


    public async Task<Stream> GetBackgroundImageAsync(string query, CancellationToken ct)
    {
        await _cacheContainer.CreateIfNotExistsAsync(cancellationToken: ct);

        var slug = Slugify(query);
        var blob = _cacheContainer.GetBlobClient($"{slug}.jpg");

        if (await blob.ExistsAsync(ct))
        {
            return await blob.OpenReadAsync(cancellationToken: ct);
        }

        byte[] bytes;

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            var httpFallback = _httpClientFactory.CreateClient(nameof(PexelsImageProvider));
            var seeded = _fallbackUrl.Replace("{seed}", Uri.EscapeDataString(slug));
            bytes = await httpFallback.GetByteArrayAsync(seeded, ct);
        }
        else
        {
            var http = _httpClientFactory.CreateClient(nameof(PexelsImageProvider));

            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(query)}&per_page=1");

            req.Headers.TryAddWithoutValidation("Authorization", _apiKey);

            using var res = await http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            var jsonText = await res.Content.ReadAsStringAsync(ct);
            using var json = JsonDocument.Parse(jsonText);

            var url = json.RootElement
                .GetProperty("photos")
                .EnumerateArray()
                .FirstOrDefault()
                .GetProperty("src")
                .GetProperty("large")
                .GetString();

            if (string.IsNullOrWhiteSpace(url))
            {
                // Pexels returned no image -> fallback
                var seeded = _fallbackUrl.Replace("{seed}", Uri.EscapeDataString(slug));
                bytes = await http.GetByteArrayAsync(seeded, ct);
            }
            else
            {
                bytes = await http.GetByteArrayAsync(url, ct);
            }
        }

        await using (var upload = new MemoryStream(bytes))
        {
            await blob.UploadAsync(upload, overwrite: true, cancellationToken: ct);
        }

        return new MemoryStream(bytes);
    }

    private static string Slugify(string input)
    {
        var slug = input.Trim().ToLowerInvariant();
        slug = string.Concat(slug.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        while (slug.Contains("__", StringComparison.Ordinal)) slug = slug.Replace("__", "_", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(slug) ? "unknown" : slug;
    }
}
