using System.Runtime.InteropServices;
using System.Text;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

/// <summary>No credentials, usernames, auth tokens, browser profiles, cookie data or full file paths.</summary>
public static class DiagnosticsReport
{
    public static string Generate(IReadOnlyList<AccountProfile> profiles, AdvancedSettings settings, LocalDeveloperApi api)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RAM 4.0 DIAGNOSTICS - SAFE TO SHARE");
        sb.AppendLine("Created UTC: " + DateTimeOffset.UtcNow.ToString("u"));
        sb.AppendLine("OS: " + RuntimeInformation.OSDescription);
        sb.AppendLine("Architecture: " + RuntimeInformation.OSArchitecture);
        sb.AppendLine("Runtime: " + RuntimeInformation.FrameworkDescription);
        sb.AppendLine("Profiles: " + profiles.Count);
        var sessions = SessionInventory.Count(profiles);
        sb.AppendLine($"Encrypted per-account logins: {sessions.WithSnapshot}; need login: {sessions.WithoutSnapshot}");
        sb.AppendLine("Important: encrypted login file presence does not establish a currently valid session.");
        try { sb.AppendLine("Roblox process count: " + RobloxService.RunningPlayerCount()); }
        catch (Exception e) { sb.AppendLine("Roblox process check error: " + e.GetType().Name); }
        try { sb.AppendLine("Roblox desktop binary detected: " + (RobloxService.FindDesktopClient() is not null)); }
        catch (Exception e) { sb.AppendLine("Roblox client detect error: " + e.GetType().Name); }
        try { sb.AppendLine("FPS XML setting: " + (FpsSettings.Read()?.ToString() ?? "Unavailable")); }
        catch (Exception e) { sb.AppendLine("FPS read error: " + e.GetType().Name); }
        sb.AppendLine("Watcher enabled: " + settings.WatcherEnabled);
        sb.AppendLine("Auto relaunch enabled: " + settings.AutoRelaunch);
        sb.AppendLine("Local API enabled: " + settings.LocalApiEnabled);
        sb.AppendLine("Local API running: " + api.IsRunning);
        sb.AppendLine("Current theme: " + settings.ThemePreset);
        sb.AppendLine("No secret/session material or account identifiers included.");
        return sb.ToString();
    }
}
