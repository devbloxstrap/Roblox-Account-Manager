using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

/// <summary>Read-only documented/public Roblox endpoint adapter. Never sends credentials.</summary>
public static class RobloxApi
{
    private static readonly HttpClient Client = CreateClient();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { Timeout = TimeSpan.FromSeconds(16) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RAMModern/4.0 (+desktop account manager)");
        return client;
    }

    private static async Task<JsonDocument> JsonAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        using (request)
        using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new InvalidOperationException("Roblox rate limit reached. Wait a little and try again.");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Roblox API returned HTTP {(int)response.StatusCode}; the endpoint might be unavailable.");
            if (response.Content.Headers.ContentLength > 2_000_000)
                throw new InvalidOperationException("Roblox response exceeded the allowed size.");
            await using var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(body, new JsonDocumentOptions { MaxDepth = 40 }, ct).ConfigureAwait(false);
        }
    }

    public static async Task<RobloxUser?> LookupUsernameAsync(string username, CancellationToken ct = default)
    {
        username = username.Trim().TrimStart('@');
        if (username.Length is < 3 or > 20 || username.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
            throw new ArgumentException("Enter a valid Roblox username (3–20 letters, digits or underscores).");
        var req = new HttpRequestMessage(HttpMethod.Post, "https://users.roblox.com/v1/usernames/users")
        { Content = new StringContent(JsonSerializer.Serialize(new { usernames = new[] { username }, excludeBannedUsers = false }), Encoding.UTF8, "application/json") };
        using var doc = await JsonAsync(req, ct);
        var rows = doc.RootElement.GetProperty("data");
        foreach (var item in rows.EnumerateArray())
        {
            string name = item.GetProperty("name").GetString() ?? "";
            if (!name.Equals(username, StringComparison.OrdinalIgnoreCase)) continue;
            var user = new RobloxUser
            {
                Id = item.GetProperty("id").GetInt64(),
                Name = name,
                DisplayName = item.GetProperty("displayName").GetString() ?? name
            };
            user.AvatarUrl = await GetAvatarAsync(user.Id, ct);
            return user;
        }
        return null;
    }

    public static async Task<RobloxUser> LookupUserIdAsync(long userId, CancellationToken ct = default)
    {
        if (userId <= 0) throw new ArgumentException("User ID must be positive.");
        using var doc = await JsonAsync(new HttpRequestMessage(HttpMethod.Get, $"https://users.roblox.com/v1/users/{userId}"), ct);
        var root = doc.RootElement;
        return new RobloxUser
        {
            Id = userId,
            Name = root.GetProperty("name").GetString() ?? "",
            DisplayName = root.GetProperty("displayName").GetString() ?? "",
            AvatarUrl = await GetAvatarAsync(userId, ct)
        };
    }

    public static async Task<string?> GetAvatarAsync(long userId, CancellationToken ct = default)
    {
        if (userId <= 0) return null;
        try
        {
            using var doc = await JsonAsync(new HttpRequestMessage(HttpMethod.Get,
                $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png&isCircular=false"), ct);
            var items = doc.RootElement.GetProperty("data");
            if (items.GetArrayLength() == 0) return null;
            string? url = items[0].GetProperty("imageUrl").GetString();
            return Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" ? url : null;
        }
        catch (HttpRequestException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (KeyNotFoundException) { return null; }
        catch (JsonException) { return null; }
    }

    public static async Task<ServerPage> GetServersAsync(long placeId, string? cursor = null, CancellationToken ct = default)
    {
        if (placeId <= 0) throw new ArgumentException("Enter a valid numeric Place ID.");
        if (cursor?.Length > 2048) throw new ArgumentException("Invalid server cursor.");
        string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?limit=100&sortOrder=Asc&excludeFullGames=true";
        if (!string.IsNullOrWhiteSpace(cursor)) url += "&cursor=" + Uri.EscapeDataString(cursor);
        using var doc = await JsonAsync(new HttpRequestMessage(HttpMethod.Get, url), ct);
        return JsonSerializer.Deserialize<ServerPage>(doc.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new ServerPage();
    }
    public static async Task<string> GetUniverseInfoAsync(long placeId, CancellationToken ct = default)
    {
        if (placeId <= 0) throw new ArgumentException("Enter a valid Place ID.");
        using var doc = await JsonAsync(new HttpRequestMessage(HttpMethod.Get,
            $"https://apis.roblox.com/universes/v1/places/{placeId}/universe"), ct);
        var root = doc.RootElement;
        if (!root.TryGetProperty("universeId", out var uid) || !uid.TryGetInt64(out long universeId))
            throw new InvalidOperationException("No universe ID was returned for this place.");
        string result = $"Place ID: {placeId}\nUniverse ID: {universeId}";
        try
        {
            using var detail = await JsonAsync(new HttpRequestMessage(HttpMethod.Get,
                $"https://games.roblox.com/v1/games?universeIds={universeId}"), ct);
            var data = detail.RootElement.GetProperty("data");
            if (data.GetArrayLength() > 0)
            {
                var game = data[0];
                string name = game.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                string plays = game.TryGetProperty("playing", out var playing) ? playing.ToString() : "?";
                string visits = game.TryGetProperty("visits", out var total) ? total.ToString() : "?";
                result += $"\nName: {name}\nCurrent players: {plays}\nTotal visits: {visits}";
            }
        }
        catch (InvalidOperationException) { /* Metadata can be unavailable while the place-to-universe endpoint works. */ }
        return result;
    }

    public static async Task<List<string>> GetPublicOutfitsAsync(long userId, CancellationToken ct = default)
    {
        if (userId <= 0) throw new ArgumentException("Enter a valid user ID.");
        using var doc = await JsonAsync(new HttpRequestMessage(HttpMethod.Get,
            $"https://avatar.roblox.com/v2/avatar/users/{userId}/outfits?itemsPerPage=50&page=1"), ct);
        var data = doc.RootElement.GetProperty("data");
        var items = new List<string>();
        foreach (var outfit in data.EnumerateArray())
        {
            string name = outfit.TryGetProperty("name", out var title) ? title.GetString() ?? "Outfit" : "Outfit";
            string outfitId = outfit.TryGetProperty("id", out var uid) ? uid.ToString() : "?";
            items.Add($"{name}  •  Outfit ID: {outfitId}");
        }
        return items;
    }

}
