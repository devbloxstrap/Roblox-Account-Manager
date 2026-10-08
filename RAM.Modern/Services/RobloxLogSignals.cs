using System.IO;
using System.Text.RegularExpressions;

namespace RAM.Modern.Services;

/// <summary>Best-effort, read-only signal for a recent client disconnection.
/// Roblox does not provide a stable external AFK/disconnect event contract.</summary>
public static partial class RobloxLogSignals
{
    [GeneratedRegex(@"(?:disconnected from (?:the )?server|lost connection|connection (?:was )?lost|network disconnect|disconnect(?:ion)? reason)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DisconnectPattern();

    public static bool SawRecentDisconnect(out string detail)
    {
        detail = "No recent Roblox disconnect signal identified.";
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
        if (!Directory.Exists(folder)) return false;
        var latest = new DirectoryInfo(folder).EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
        if (latest is null || DateTime.UtcNow - latest.LastWriteTimeUtc > TimeSpan.FromSeconds(30)) return false;
        try
        {
            using var stream = new FileStream(latest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            int take = (int)Math.Min(stream.Length, 8 * 1024);
            stream.Seek(-take, SeekOrigin.End);
            var data = new byte[take];
            int read = stream.Read(data, 0, data.Length);
            string tail = System.Text.Encoding.UTF8.GetString(data, 0, read);
            if (!DisconnectPattern().IsMatch(tail)) return false;
            detail = "A disconnect-like message was detected in the recently written Roblox log. It is not an official AFK signal.";
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
