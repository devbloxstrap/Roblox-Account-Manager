using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

/// <summary>Validates the current Windows Roblox client session ONLY against the official Roblox users endpoint.
/// Never logs or persists extracted cookie values. Requires user permission in the UI.</summary>
public static class RobloxSessionIdentity
{
    private static readonly HttpClient Client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(12) };

    public static async Task<RobloxUser?> CheckCurrentAsync(CancellationToken token = default)
    {
        if (!File.Exists(SessionSwitcher.LivePath)) return null;
        byte[]? decrypted = null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(SessionSwitcher.LivePath));
            if (!document.RootElement.TryGetProperty("CookiesData", out var encoded) || encoded.ValueKind != JsonValueKind.String)
                return null;
            string? data = encoded.GetString();
            if (string.IsNullOrEmpty(data) || data.Length > 2_000_000) return null;
            decrypted = ProtectedData.Unprotect(Convert.FromBase64String(data), null, DataProtectionScope.CurrentUser);
            string raw = Encoding.UTF8.GetString(decrypted);
            var match = Regex.Match(raw, "(_\\|WARNING:-DO-NOT-SHARE[^\\s;,\\\"']+)");
            if (!match.Success) match = Regex.Match(raw, "\\.ROBLOSECURITY[\\s=]+([^\\s;,\\\"']+)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;
            string cookie = match.Groups[1].Value;
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
            req.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + cookie);
            using var response = await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength > 65536) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var identity = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            var root = identity.RootElement;
            return new RobloxUser
            {
                Id = root.GetProperty("id").GetInt64(),
                Name = root.GetProperty("name").GetString() ?? "",
                DisplayName = root.GetProperty("displayName").GetString() ?? ""
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or CryptographicException or JsonException)
        {
            return null;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Could not reach Roblox's authentication service; try again when network access is available.", ex);
        }
        catch (TaskCanceledException ex) when (!token.IsCancellationRequested)
        {
            throw new InvalidOperationException("Roblox session verification timed out. Try again.", ex);
        }
        finally { if (decrypted is not null) CryptographicOperations.ZeroMemory(decrypted); }
    }
}
