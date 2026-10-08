using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace RAM.Modern.Services;

/// <summary>Explicit, user-initiated import from a NEW isolated Edge/Chrome window.
/// Only the Roblox session cookie is read, after the user presses Capture.
/// Never attaches to the user's existing browser profile or reads a password.</summary>
public sealed class BrowserLoginCapture : IDisposable
{
    private readonly string _tempFolder = Path.Combine(Path.GetTempPath(), "RAMModern-Login-" + Guid.NewGuid().ToString("N"));
    private readonly HttpClient _localHttp = new() { Timeout = TimeSpan.FromSeconds(3) };
    private Process? _browser;
    private int _port;
    private bool _disposed;
    public string EngineName { get; private set; } = "Browser";

    public void Start()
    {
        string? edge = FindBrowser();
        if (edge == null) throw new FileNotFoundException("Microsoft Edge or Google Chrome not found. Install an official browser to add your Roblox account.");
        EngineName = Path.GetFileName(edge).Equals("msedge.exe", StringComparison.OrdinalIgnoreCase) ? "Microsoft Edge" : "Google Chrome";
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { _port = ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
        Directory.CreateDirectory(_tempFolder);
        var psi = new ProcessStartInfo(edge)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(edge)!
        };
        psi.ArgumentList.Add("--user-data-dir=" + _tempFolder);
        psi.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
        psi.ArgumentList.Add("--remote-debugging-port=" + _port);
        psi.ArgumentList.Add("--no-first-run");
        psi.ArgumentList.Add("--no-default-browser-check");
        psi.ArgumentList.Add("--app=https://www.roblox.com/login");
        _browser = Process.Start(psi) ?? throw new InvalidOperationException("The browser did not start.");
    }

    private static string? FindBrowser()
    {
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new[]
        {
            Path.Combine(x86, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(local, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(pf, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(x86, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe")
        }.FirstOrDefault(File.Exists);
    }

    public async Task<string> CaptureCookieAsync(CancellationToken ct = default)
    {
        if (_browser == null || _disposed) throw new InvalidOperationException("Start the official login browser first.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        Uri? socketUri = null;
        for (int attempt = 0; attempt < 25; attempt++)
        {
            timeout.Token.ThrowIfCancellationRequested();
            try
            {
                string version = await _localHttp.GetStringAsync($"http://127.0.0.1:{_port}/json/version", timeout.Token);
                using var json = JsonDocument.Parse(version);
                string? ws = json.RootElement.GetProperty("webSocketDebuggerUrl").GetString();
                if (Uri.TryCreate(ws, UriKind.Absolute, out var endpoint) && endpoint.Scheme == "ws" &&
                    endpoint.Port == _port && (endpoint.Host == "localhost" || endpoint.Host == "127.0.0.1"))
                { socketUri = endpoint; break; }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
            await Task.Delay(350, timeout.Token);
        }
        if (socketUri == null)
            throw new InvalidOperationException("Could not connect to the isolated login browser. Close this dialog and try again. Your normal browser is untouched.");
        using var wsClient = new ClientWebSocket();
        await wsClient.ConnectAsync(socketUri, timeout.Token);
        byte[] command = Encoding.UTF8.GetBytes("{\"id\":17,\"method\":\"Storage.getCookies\"}");
        await wsClient.SendAsync(command.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
        using var result = new MemoryStream();
        byte[] buffer = new byte[8192];
        for (int i = 0; i < 40; i++)
        {
            result.SetLength(0);
            WebSocketReceiveResult info;
            do
            {
                info = await wsClient.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                if (info.MessageType == WebSocketMessageType.Close)
                    throw new IOException("Browser closed before the account login could be read.");
                result.Write(buffer, 0, info.Count);
                if (result.Length > 1_048_576) throw new InvalidDataException("Browser response too large.");
            } while (!info.EndOfMessage);
            using var payload = JsonDocument.Parse(result.ToArray());
            var root = payload.RootElement;
            if (!root.TryGetProperty("id", out var id) || id.GetInt32() != 17) continue;
            if (root.TryGetProperty("error", out _))
                throw new InvalidOperationException("This browser does not allow account session capture. Try a newer Microsoft Edge.");
            if (!root.TryGetProperty("result", out var resultJson) ||
                !resultJson.TryGetProperty("cookies", out var cookies)) break;
            foreach (var item in cookies.EnumerateArray())
            {
                if (item.GetProperty("name").GetString() != ".ROBLOSECURITY") continue;
                string domain = item.GetProperty("domain").GetString() ?? "";
                if (domain != "roblox.com" && domain != ".roblox.com") continue;
                string secret = item.GetProperty("value").GetString() ?? "";
                if (secret.Length > 20 && secret.Length < 8192) return secret;
            }
            break;
        }
        throw new InvalidOperationException("No Roblox login session was found in this browser. Finish login (including verification/2FA), then click Capture again. Do not log out.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localHttp.Dispose();
        try
        {
            if (_browser != null)
            {
                if (!_browser.HasExited)
                {
                    _browser.CloseMainWindow();
                    if (!_browser.WaitForExit(2500)) _browser.Kill(entireProcessTree: true);
                }
                _browser.Dispose();
            }
        }
        catch { /* May have already been closed. */ }
        // Best effort cleanup. No Roblox secrets are intentionally persisted here.
        try { if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, recursive: true); }
        catch { /* Chromium can keep locks briefly after process exit. */ }
    }
}
