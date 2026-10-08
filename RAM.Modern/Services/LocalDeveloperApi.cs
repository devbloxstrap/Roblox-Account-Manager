using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

/// <summary>Explicit opt-in loopback-only API. All endpoints require a random bearer token.
/// Never exposes cookies, sessions, passwords or raw file paths.</summary>
public sealed class LocalDeveloperApi : IDisposable
{
    private static string KeyFile => Path.Combine(ProfileStore.Folder, "api-token.dpapi");
    private static readonly byte[] Entropy = "RAM.Modern.LocalApi.v1"u8.ToArray();
    private readonly Func<IReadOnlyList<AccountProfile>> _profiles;
    private readonly Func<bool> _robloxRunning;
    private HttpListener? _listener;
    private CancellationTokenSource? _stop;
    public bool IsRunning => _listener?.IsListening == true;
    public int Port { get; private set; }
    public event Action<string>? StatusChanged;

    public LocalDeveloperApi(Func<IReadOnlyList<AccountProfile>> profiles, Func<bool> robloxRunning)
    {
        _profiles = profiles;
        _robloxRunning = robloxRunning;
    }

    private static byte[] ReadOrCreateToken()
    {
        Directory.CreateDirectory(ProfileStore.Folder);
        if (File.Exists(KeyFile))
        {
            byte[] token = ProtectedData.Unprotect(File.ReadAllBytes(KeyFile), Entropy, DataProtectionScope.CurrentUser);
            if (token.Length == 32) return token;
            CryptographicOperations.ZeroMemory(token);
            throw new InvalidDataException("Local API key data is invalid. Reset it from Settings.");
        }
        var newToken = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(KeyFile, ProtectedData.Protect(newToken, Entropy, DataProtectionScope.CurrentUser));
        return newToken;
    }

    public static string GetTokenHex()
    {
        var bytes = ReadOrCreateToken();
        try { return Convert.ToHexString(bytes).ToLowerInvariant(); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static void ResetToken() { if (File.Exists(KeyFile)) File.Delete(KeyFile); _ = GetTokenHex(); }

    public void Start(int port)
    {
        if (IsRunning && Port == port) return;
        Stop();
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        using (var token = new SensitiveToken(ReadOrCreateToken())) { /* Confirm key can be decrypted before opening the listener. */ }
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        _listener = listener;
        _stop = new CancellationTokenSource();
        Port = port;
        _ = LoopAsync(listener, _stop.Token);
        StatusChanged?.Invoke($"Local API listening on 127.0.0.1:{port} (token required).");
    }

    private sealed class SensitiveToken : IDisposable
    {
        public readonly byte[] Value;
        public SensitiveToken(byte[] value) => Value = value;
        public void Dispose() => CryptographicOperations.ZeroMemory(Value);
    }

    private async Task LoopAsync(HttpListener listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && listener.IsListening)
            {
                var context = await listener.GetContextAsync().WaitAsync(ct);
                _ = HandleAsync(context);
            }
        }
        catch (OperationCanceledException) { }
        catch (HttpListenerException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { StatusChanged?.Invoke("Local API stopped: " + ex.Message); }
    }

    private static async Task WriteAsync(HttpListenerResponse response, int status, object obj)
    {
        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(obj);
            response.StatusCode = status;
            response.ContentType = "application/json; charset=utf-8";
            response.Headers.Add("Cache-Control", "no-store");
            response.Headers.Add("X-Content-Type-Options", "nosniff");
            response.ContentLength64 = json.Length;
            await response.OutputStream.WriteAsync(json);
        }
        finally { response.Close(); }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            // Reject browser-based calls and hostile hosts even if a token is supplied.
            if (!IPAddress.IsLoopback(req.RemoteEndPoint?.Address ?? IPAddress.None) ||
                !string.Equals(req.Url?.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(req.Headers["Origin"]))
            { await WriteAsync(ctx.Response, 403, new { error = "Loopback client only" }); return; }

            string authorization = req.Headers["Authorization"] ?? "";
            string presented = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization[7..].Trim() : "";
            byte[]? candidate = null;
            try { if (presented.Length == 64) candidate = Convert.FromHexString(presented); } catch (FormatException) { }
            using var expected = new SensitiveToken(ReadOrCreateToken());
            bool authorized = candidate is { Length: 32 } && CryptographicOperations.FixedTimeEquals(candidate, expected.Value);
            if (candidate is not null) CryptographicOperations.ZeroMemory(candidate);
            if (!authorized) { await WriteAsync(ctx.Response, 401, new { error = "Valid bearer token required" }); return; }

            string path = req.Url?.AbsolutePath.TrimEnd('/') ?? "";
            if (req.HttpMethod == "GET" && path == "/v1/status")
            {
                await WriteAsync(ctx.Response, 200, new { app = "RAM Modern", version = "4.0-preview.6", robloxRunning = _robloxRunning(), profiles = _profiles().Count });
            }
            else if (req.HttpMethod == "GET" && path == "/v1/profiles")
            {
                var safe = _profiles().Select(p => new { id = p.Id, username = p.Username, displayName = p.DisplayName, userId = p.UserId, group = p.Group, favorite = p.Favorite }).ToList();
                await WriteAsync(ctx.Response, 200, new { profiles = safe });
            }
            else if (req.HttpMethod == "POST" && path.StartsWith("/v1/open-game/", StringComparison.Ordinal) &&
                     long.TryParse(path["/v1/open-game/".Length..], out long placeId) && placeId > 0)
            {
                // This opens an official Roblox game page using the current Windows session, never an authenticated hidden action.
                RobloxService.OpenGame(placeId);
                await WriteAsync(ctx.Response, 200, new { requested = true, placeId, authenticated = false });
            }
            else if (req.HttpMethod == "POST" && path == "/v1/desktop/launch")
            {
                var message = RobloxService.LaunchDesktop();
                await WriteAsync(ctx.Response, 200, new { requested = true, message, authenticated = false });
            }
            else if (req.HttpMethod == "POST" && path.StartsWith("/v1/open-profile/", StringComparison.Ordinal) &&
                     long.TryParse(path["/v1/open-profile/".Length..], out long userId) && userId > 0)
            {
                RobloxService.OpenProfile(userId);
                await WriteAsync(ctx.Response, 200, new { requested = true, userId, authenticated = false });
            }
            else if (req.HttpMethod == "POST" && path.StartsWith("/v1/account-browser/", StringComparison.Ordinal) &&
                     Guid.TryParse(path["/v1/account-browser/".Length..], out Guid profileId) &&
                     _profiles().Any(p => p.Id == profileId))
            {
                RobloxService.OpenIsolatedAccountBrowser(profileId);
                await WriteAsync(ctx.Response, 200, new { requested = true, profileId, authenticated = false });
            }
            else await WriteAsync(ctx.Response, 404, new { error = "Unknown API route" });
        }
        catch (Exception ex)
        {
            try { await WriteAsync(ctx.Response, 500, new { error = ex.Message }); }
            catch { try { ctx.Response.Close(); } catch { } }
        }
    }

    public void Stop()
    {
        var stop = _stop;
        _stop = null;
        stop?.Cancel();
        _listener?.Close();
        _listener = null;
        stop?.Dispose();
    }

    public void Dispose() => Stop();
}
