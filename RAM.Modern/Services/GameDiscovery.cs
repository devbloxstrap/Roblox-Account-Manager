using System.Net.Http;
using System.Text.Json;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

public sealed record DiscoveredGame(long PlaceId, long UniverseId, string Name, string Description, int Playing)
{
    public string Summary => $"{Name} • {Playing:N0} players • Place {PlaceId}";
}

public sealed record SmallServerResult(List<GameServer> Servers, int ScannedPages, bool MorePagesExist);

/// <summary>Public, read-only Roblox discovery with bounded scans and timeouts.
/// Recommendations may be unavailable as the API evolves.</summary>
public static class GameDiscovery
{
    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(15) };

    static GameDiscovery() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("RAMModern/4.0");

    private static async Task<JsonDocument> GetJson(string url, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Roblox discovery endpoint unavailable (HTTP {(int)response.StatusCode}).");
        if (response.Content.Headers.ContentLength is > 2_000_000) throw new InvalidDataException("Roblox response too large.");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(input, new JsonDocumentOptions { MaxDepth = 32 }, ct);
    }

    public static async Task<SmallServerResult> FindSmallServersAsync(long placeId, int maxPages = 5, CancellationToken ct = default)
    {
        if (placeId <= 0 || maxPages is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(placeId));
        var servers = new Dictionary<string, GameServer>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        int pages = 0;
        do
        {
            ct.ThrowIfCancellationRequested();
            var page = await RobloxApi.GetServersAsync(placeId, cursor, ct);
            pages++;
            foreach (var s in page.Data ?? new List<GameServer>())
                if (s.Playing < s.MaxPlayers && !string.IsNullOrWhiteSpace(s.Id)) servers[s.Id] = s;
            cursor = page.NextPageCursor;
            if (string.IsNullOrWhiteSpace(cursor) || !visited.Add(cursor)) break;
            if (pages < maxPages) await Task.Delay(350, ct); // avoid hammering the service
        } while (pages < maxPages);
        return new SmallServerResult(servers.Values.OrderBy(s => s.Playing).ThenBy(s => s.Ping ?? double.MaxValue).ToList(), pages, !string.IsNullOrWhiteSpace(cursor) && pages >= maxPages);
    }

    public static async Task<List<DiscoveredGame>> RecommendAsync(long placeId, CancellationToken ct = default)
    {
        if (placeId <= 0) throw new ArgumentException("Enter a positive Place ID.");
        using var universeDocument = await GetJson($"https://apis.roblox.com/universes/v1/places/{placeId}/universe", ct);
        if (!universeDocument.RootElement.TryGetProperty("universeId", out var universeField) || !universeField.TryGetInt64(out long universeId) || universeId <= 0)
            throw new InvalidOperationException("No universe found for this game.");
        using var result = await GetJson($"https://games.roblox.com/v1/games/recommendations/game/{universeId}?maxRows=20", ct);
        if (!result.RootElement.TryGetProperty("games", out var rows) && !result.RootElement.TryGetProperty("data", out rows))
            throw new InvalidDataException("Recommendation response format changed.");
        if (rows.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Recommendations were not a list.");
        var games = new List<DiscoveredGame>();
        foreach (var row in rows.EnumerateArray())
        {
            long recommendedUniverse = Number(row, "universeId");
            long rootPlace = Number(row, "rootPlaceId");
            if (rootPlace <= 0 && row.TryGetProperty("rootPlace", out var root) && root.ValueKind == JsonValueKind.Object)
                rootPlace = Number(root, "id");
            if (rootPlace <= 0) continue;
            string name = row.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "Unnamed" : "Unnamed";
            string desc = row.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? "" : "";
            games.Add(new DiscoveredGame(rootPlace, recommendedUniverse, name, desc, (int)Math.Clamp(Number(row, "playing"), 0, int.MaxValue)));
        }
        return games.GroupBy(g => g.PlaceId).Select(g => g.First()).ToList();
    }

    private static long Number(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long n)) return n;
        return 0;
    }
}
