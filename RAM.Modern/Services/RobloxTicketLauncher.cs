using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

/// <summary>Roblox's own account identity and game-launch ticket handoff.
/// All secrets stay in memory and requests go ONLY to official Roblox HTTPS endpoints.</summary>
public static class RobloxTicketLauncher
{
    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(20) };

    static RobloxTicketLauncher() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("Roblox/WinInet");

    private static void AuthHeaders(HttpRequestMessage request, string cookie)
    {
        request.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + cookie);
        request.Headers.Referrer = new Uri("https://www.roblox.com/home");
    }

    public static async Task<RobloxUser?> VerifyAsync(string cookie, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cookie)) return null;
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
        AuthHeaders(req, cookie);
        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        if (resp.StatusCode == HttpStatusCode.TooManyRequests) throw new InvalidOperationException("Roblox is rate-limiting account verification. Try later.");
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"Roblox identity service returned HTTP {(int)resp.StatusCode}.");
        if (resp.Content.Headers.ContentLength > 65536) throw new InvalidDataException("Identity response too large.");
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 16 }, ct);
        var root = doc.RootElement;
        long id = root.GetProperty("id").GetInt64();
        string username = root.GetProperty("name").GetString() ?? "";
        if (id <= 0 || username.Length == 0) throw new InvalidDataException("Roblox returned an invalid identity.");
        return new RobloxUser { Id = id, Name = username, DisplayName = root.TryGetProperty("displayName", out var name) ? name.GetString() ?? username : username };
    }

    public static async Task<RobloxUser> VerifySavedAsync(AccountProfile profile, CancellationToken ct = default)
    {
        string cookie = AccountAuthStore.Load(profile.Id);
        var actual = await VerifyAsync(cookie, ct);
        if (actual == null) throw new InvalidOperationException($"Login for @{profile.Username} has expired or was revoked. Use Add via browser to re-login ONLY this account.");
        if ((profile.UserId > 0 && actual.Id != profile.UserId) ||
            !profile.Username.Equals(actual.Name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Saved login identity mismatch: selected @{profile.Username}, but Roblox returned @{actual.Name}. No launch attempted.");
        return actual;
    }

    public static string BuildLaunchUri(string ticket, long placeId, string? jobId, long launchTime, int browserTracker)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 4096 || ticket.Any(ch => ch is '+' or '\r' or '\n' or ':' or ' '))
            throw new ArgumentException("Invalid Roblox launch ticket.");
        if (placeId <= 0) throw new ArgumentOutOfRangeException(nameof(placeId));
        if (browserTracker <= 0 || launchTime <= 0) throw new ArgumentException("Invalid launch parameters.");
        string query;
        if (string.IsNullOrWhiteSpace(jobId))
            query = $"request=RequestGame&browserTrackerId={browserTracker}&placeId={placeId}&isPlayTogetherGame=false";
        else
        {
            if (!Guid.TryParse(jobId, out Guid parsed)) throw new ArgumentException("Job ID must be a Roblox server GUID.");
            query = $"request=RequestGameJob&browserTrackerId={browserTracker}&placeId={placeId}&gameId={parsed:D}&isPlayTogetherGame=false";
        }
        string launcherUrl = "https://assetgame.roblox.com/game/PlaceLauncher.ashx?" + query;
        return $"roblox-player:1+launchmode:play+gameinfo:{ticket}+launchtime:{launchTime}+placelauncherurl:{WebUtility.UrlEncode(launcherUrl)}+browsertrackerid:{browserTracker}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
    }

    private static async Task<(string Ticket, string? RotatedCookie)> FetchTicketAsync(string cookie, CancellationToken ct)
    {
        string? csrf = null;
        for (int n = 0; n < 3; n++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/");
            AuthHeaders(req, cookie);
            if (csrf != null) req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            req.Content = new StringContent("", Encoding.UTF8, "application/json");
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.StatusCode == HttpStatusCode.Forbidden && resp.Headers.TryGetValues("x-csrf-token", out var tokens))
            {
                csrf = tokens.FirstOrDefault();
                if (string.IsNullOrEmpty(csrf)) break;
                continue;
            }
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new InvalidOperationException("Roblox rejected this account session. Re-login this account in the browser.");
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                throw new InvalidOperationException("Roblox rate limit reached; wait before another launch.");
            if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"Roblox ticket endpoint returned HTTP {(int)resp.StatusCode}.");
            if (resp.Headers.TryGetValues("rbx-authentication-ticket", out var values))
            {
                string? ticket = values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(ticket))
                {
                    string? rotated = null;
                    if (resp.Headers.TryGetValues("Set-Cookie", out var setCookies))
                    {
                        foreach (string header in setCookies)
                        {
                            const string prefix = ".ROBLOSECURITY=";
                            if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                            string value = header[prefix.Length..].Split(';', 2)[0].Trim();
                            if (value.Length is > 20 and < 8192 && value != cookie) rotated = value;
                        }
                    }
                    return (ticket, rotated);
                }
            }
            break;
        }
        throw new InvalidOperationException("Roblox did not issue a fresh authentication ticket. This Roblox client/API version may be incompatible.");
    }

    public static async Task<string> LaunchAsync(AccountProfile profile, long placeId, string? jobId = null, CancellationToken ct = default)
    {
        if (RobloxService.RunningPlayerCount() != 0)
            throw new InvalidOperationException("Close the current Roblox Player before launching a different account. Multi-instance is disabled.");
        if (placeId <= 0) throw new ArgumentException("Set a valid game Place ID. Authentication tickets launch games, not the desktop Home screen.");
        // Fail before requesting a short-lived ticket if Windows has no Roblox handler.
        if (RobloxService.FindDesktopClient() == null)
            throw new FileNotFoundException("Roblox Player not detected. Install/open the official desktop client once.");
        string cookie = AccountAuthStore.Load(profile.Id);
        var current = await VerifyAsync(cookie, ct);
        if (current == null) throw new InvalidOperationException($"@{profile.Username}'s login expired. Re-login that account via Add via browser.");
        if ((profile.UserId > 0 && profile.UserId != current.Id) ||
            !profile.Username.Equals(current.Name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Saved login belongs to a different account. No launch attempted.");
        var (ticket, rotated) = await FetchTicketAsync(cookie, ct);
        int tracker = RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
        string uri = BuildLaunchUri(ticket, placeId, jobId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), tracker);
        if (rotated != null)
        {
            // A rotated session is persisted only after validating it still belongs to this account.
            var rotatedIdentity = await VerifyAsync(rotated, ct);
            if (rotatedIdentity == null || rotatedIdentity.Id != current.Id)
                throw new InvalidOperationException("Roblox rotated the login but could not verify its identity. No launch attempted.");
            AccountAuthStore.Save(profile.Id, rotated);
        }
        // Never log the ticket-bearing URI. Only the Windows registered handler receives it.
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        return $"Sent Roblox game {placeId} launch for @{current.Name} to Windows. Verify actual client identity in game.";
    }
}
