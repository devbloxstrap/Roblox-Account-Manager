using System.Diagnostics;
using System.IO;

namespace RAM.Modern.Services;

public static class RobloxService
{
    public static int RunningPlayerCount()
    {
        int count = 0;
        foreach (string name in new[] { "RobloxPlayerBeta", "RobloxPlayer" })
        {
            var processes = Process.GetProcessesByName(name);
            count += processes.Length;
            foreach (var process in processes) process.Dispose();
        }
        return count;
    }

    public static string? FindDesktopClient()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
        if (!Directory.Exists(root)) return null;
        // Prefer the most recently modified installed Roblox player. Never download or execute an unknown binary.
        return Directory.EnumerateFiles(root, "RobloxPlayerBeta.exe", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    public static string LaunchDesktop()
    {
        if (RunningPlayerCount() > 0) return "Roblox Player is already running. Multi-instance launching is disabled.";
        var exe = FindDesktopClient();
        if (exe is null)
            throw new FileNotFoundException("The Roblox desktop player was not found in the standard Windows installation folder. Install or launch Roblox once through its official website, then retry.");
        // Launching a Roblox binary without a game/auth handoff may display login or exit,
        // depending on the installed Roblox version. This does not authenticate a session.
        Process.Start(new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = true
        });
        return "Roblox desktop launch requested. Check the client window; authentication is not yet verified.";
    }

    public static void Login() => LaunchDesktop();
    public static void BrowserLogin() => Open("https://www.roblox.com/login");
    public static void Home() => Open("https://www.roblox.com/home");
    public static void OpenProfile(long userId)
    {
        if (userId <= 0) throw new ArgumentException("Add a valid Roblox numeric user ID first.");
        Open($"https://www.roblox.com/users/{userId}/profile");
    }
    public static void OpenGame(long placeId)
    {
        if (placeId <= 0) throw new ArgumentException("Enter a valid Roblox place ID.");
        Open($"https://www.roblox.com/games/{placeId}");
    }
    /// <summary>Opens a separate Edge browser profile for each saved account.
    /// The user authenticates directly with Roblox in Edge; no cookies are imported or shared.</summary>
    public static void OpenIsolatedAccountBrowser(Guid accountId, string destination = "https://www.roblox.com/login")
    {
        if (!Uri.TryCreate(destination, UriKind.Absolute, out var target) || target.Scheme != Uri.UriSchemeHttps ||
            !(target.Host.Equals("roblox.com", StringComparison.OrdinalIgnoreCase) || target.Host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Only official Roblox HTTPS pages may be opened in an account browser.");
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe")
        };
        string? edge = candidates.FirstOrDefault(File.Exists);
        if (edge is null) throw new FileNotFoundException("Microsoft Edge could not be located. Install Edge, then retry account-specific browser opening.");
        string folder = Path.Combine(ProfileStore.Folder, "browser-profiles", accountId.ToString("N"));
        Directory.CreateDirectory(folder);
        var start = new ProcessStartInfo(edge) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(edge)! };
        start.ArgumentList.Add("--user-data-dir=" + folder);
        start.ArgumentList.Add("--no-first-run");
        start.ArgumentList.Add(destination);
        Process.Start(start);
    }

    /// <summary>Only Edge's own built-in password manager handles passwords. RAM cannot read them.</summary>
    public static void OpenIsolatedAccountPasswordManager(Guid accountId)
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe")
        };
        string? edge = candidates.FirstOrDefault(File.Exists);
        if (edge is null) throw new FileNotFoundException("Microsoft Edge could not be found.");
        string folder = Path.Combine(ProfileStore.Folder, "browser-profiles", accountId.ToString("N"));
        Directory.CreateDirectory(folder);
        var start = new ProcessStartInfo(edge) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(edge)! };
        start.ArgumentList.Add("--user-data-dir=" + folder);
        start.ArgumentList.Add("--no-first-run");
        start.ArgumentList.Add("edge://settings/passwords");
        Process.Start(start);
    }

    public static void OpenServer(long placeId, string serverId)
    {
        if (placeId <= 0 || !Guid.TryParse(serverId, out var parsed))
            throw new ArgumentException("A valid Place ID and Roblox server Job ID are required.");
        // Official Roblox deep link. Client handling may vary by version.
        Open($"roblox://experiences/start?placeId={placeId}&gameInstanceId={parsed:D}");
    }

    public static void OpenPrivateServerLink(string address)
    {
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
            !(url.Host.Equals("roblox.com", StringComparison.OrdinalIgnoreCase) || url.Host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Only official https://*.roblox.com private server links are accepted.");
        Open(url.ToString());
    }

    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
