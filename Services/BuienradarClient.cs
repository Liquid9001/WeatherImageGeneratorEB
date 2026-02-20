using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WeatherImageGenerator.Models;

namespace WeatherImageGenerator.Services;

public sealed class BuienradarClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiUrl;

    public BuienradarClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _apiUrl = configuration["BUIENRADAR_API"] ?? "https://data.buienradar.nl/2.0/feed/json";
    }

    public async Task<IReadOnlyList<StationReading>> GetStationReadingsAsync(CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient(nameof(BuienradarClient));
        using var res = await http.GetAsync(_apiUrl, ct);
        res.EnsureSuccessStatusCode();

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        // Try common Buienradar paths first
        var candidates = new List<JsonElement>();

        if (TryGetArray(doc.RootElement, new[] { "actual", "stationmeasurements" }, out var arr)) candidates.AddRange(arr.EnumerateArray());
        else if (TryGetArray(doc.RootElement, new[] { "actual", "stationMeasurements" }, out arr)) candidates.AddRange(arr.EnumerateArray());

        if (candidates.Count == 0)
        {
            // Fallback: find the first array of objects that looks like station measurements
            var found = FindFirstLikelyStationArray(doc.RootElement);
            if (found.HasValue)
                candidates.AddRange(found.Value.EnumerateArray());
        }

        var readings = new List<StationReading>();
        foreach (var el in candidates)
        {
            if (el.ValueKind != JsonValueKind.Object) continue;

            var name = GetString(el, "stationname") ?? GetString(el, "stationName") ?? GetString(el, "name");
            var tempRaw = GetString(el, "temperature") ?? GetString(el, "temperatureC") ?? GetString(el, "temp");
            var cond = GetString(el, "weatherdescription") ?? GetString(el, "weatherDescription")
                       ?? GetString(el, "weatherdescriptionlong") ?? GetString(el, "weatherDescriptionLong")
                       ?? GetString(el, "description")
                       ?? "";

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tempRaw))
                continue;

            if (!TryParseDouble(tempRaw, out var tempC))
                continue;

            readings.Add(new StationReading
            {
                StationName = name,
                TemperatureC = tempC,
                Condition = cond
            });
        }

        return readings;
    }

    private static bool TryGetArray(JsonElement root, string[] path, out JsonElement array)
    {
        array = default;
        var cur = root;
        foreach (var p in path)
        {
            if (cur.ValueKind != JsonValueKind.Object) return false;
            if (!TryGetPropertyIgnoreCase(cur, p, out cur)) return false;
        }

        if (cur.ValueKind != JsonValueKind.Array) return false;
        array = cur;
        return true;
    }

    private static JsonElement? FindFirstLikelyStationArray(JsonElement el)
    {
        // Depth-first search
        if (el.ValueKind == JsonValueKind.Array)
        {
            var first = el.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object)
            {
                // Heuristic: contains station + temperature fields
                var hasStation = first.EnumerateObject().Any(p => p.Name.Equals("stationname", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("stationName", StringComparison.OrdinalIgnoreCase));
                var hasTemp = first.EnumerateObject().Any(p => p.Name.Equals("temperature", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("temperatureC", StringComparison.OrdinalIgnoreCase));
                if (hasStation && hasTemp) return el;
            }
        }

        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                var found = FindFirstLikelyStationArray(prop.Value);
                if (found.HasValue) return found;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement obj, string name)
    {
        if (!TryGetPropertyIgnoreCase(obj, name, out var prop)) return null;
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null
        };
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static bool TryParseDouble(string raw, out double value)
    {
        // Buienradar sometimes uses comma decimals.
        raw = raw.Trim();
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
        if (double.TryParse(raw, NumberStyles.Float, new CultureInfo("nl-NL"), out value)) return true;
        return false;
    }
}
