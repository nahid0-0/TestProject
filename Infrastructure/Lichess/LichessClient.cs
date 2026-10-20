using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chess.Application.Contracts.External;
using Chess.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Chess.Infrastructure.Lichess;

public static class LichessJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed class LichessClient(HttpClient http, ILogger<LichessClient> log) : ILichessClient
{
    public async IAsyncEnumerable<RawGame> StreamGamesAsync(
        string username, GameQuery q, [EnumeratorCancellation] CancellationToken ct)
    {
        var url = $"api/games/user/{Uri.EscapeDataString(username)}?{BuildQueryString(q)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd("application/x-ndjson");

        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            RawGame? game = null;
            try
            {
                game = JsonSerializer.Deserialize<RawGame>(line, LichessJson.Options);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to deserialize ndjson line from Lichess");
            }
            if (game is not null) yield return game;
        }
    }

    public async Task<LichessProfile?> GetProfileAsync(string username, CancellationToken ct)
    {
        var url = $"api/user/{Uri.EscapeDataString(username)}";
        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<LichessProfile>(json, LichessJson.Options);
    }

    public async Task<IReadOnlyList<RatingPoint>> GetRatingHistoryAsync(string username, CancellationToken ct)
    {
        var url = $"api/user/{Uri.EscapeDataString(username)}/rating-history";
        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return Array.Empty<RatingPoint>();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var list = new List<RatingPoint>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var name = element.GetProperty("name").GetString() ?? "";
            var cat = name.ToLowerInvariant() switch
            {
                "bullet" => TimeControlCategory.Bullet,
                "blitz" => TimeControlCategory.Blitz,
                "rapid" => TimeControlCategory.Rapid,
                "classical" => TimeControlCategory.Classical,
                "correspondence" => TimeControlCategory.Correspondence,
                _ => TimeControlCategory.Blitz
            };

            if (element.TryGetProperty("points", out var pointsArray))
            {
                foreach (var pt in pointsArray.EnumerateArray())
                {
                    int year = pt[0].GetInt32();
                    int month = pt[1].GetInt32() + 1;
                    int day = pt[2].GetInt32();
                    int rating = pt[3].GetInt32();
                    list.Add(new RatingPoint(new DateTime(year, month, day), rating, cat));
                }
            }
        }

        return list;
    }

    private static string BuildQueryString(GameQuery q)
    {
        var parameters = new List<string> { "pgnInJson=false" };
        if (q.SinceMs.HasValue) parameters.Add($"since={q.SinceMs.Value}");
        if (q.UntilMs.HasValue) parameters.Add($"until={q.UntilMs.Value}");
        if (q.Max.HasValue) parameters.Add($"max={q.Max.Value}");
        if (q.Rated.HasValue) parameters.Add($"rated={q.Rated.Value.ToString().ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(q.PerfType)) parameters.Add($"perfType={Uri.EscapeDataString(q.PerfType)}");
        parameters.Add($"clocks={q.Clocks.ToString().ToLowerInvariant()}");
        parameters.Add($"evals={q.Evals.ToString().ToLowerInvariant()}");
        parameters.Add($"opening={q.Opening.ToString().ToLowerInvariant()}");

        return string.Join("&", parameters);
    }
}
