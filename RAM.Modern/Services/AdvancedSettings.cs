using System.IO;
using System.Text.Json;

namespace RAM.Modern.Services;

public sealed class AdvancedSettings
{
    public bool WatcherEnabled { get; set; } = true;
    public bool AutoRelaunch { get; set; } = false;
    public bool WatcherNotifications { get; set; } = true;
    public bool RequireDisconnectSignal { get; set; } = true;
    public string ThemePreset { get; set; } = "Aurora";
    public string CustomAccent { get; set; } = "#4D9CFF";
    public long RelaunchPlaceId { get; set; }
    public bool LocalApiEnabled { get; set; } = false;
    public int LocalApiPort { get; set; } = 38741;
    public bool DeveloperMode { get; set; } = false;
}

public static class SettingsStore
{
    public static string FilePath => Path.Combine(ProfileStore.Folder, "settings.json");

    public static AdvancedSettings Load()
    {
        if (!File.Exists(FilePath)) return new AdvancedSettings();
        var settings = JsonSerializer.Deserialize<AdvancedSettings>(File.ReadAllText(FilePath)) ?? new AdvancedSettings();
        if (settings.LocalApiPort is < 1024 or > 65535) settings.LocalApiPort = 38741;
        if (settings.RelaunchPlaceId < 0) settings.RelaunchPlaceId = 0;
        return settings;
    }

    public static void Save(AdvancedSettings settings)
    {
        Directory.CreateDirectory(ProfileStore.Folder);
        if (settings.LocalApiPort is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(settings.LocalApiPort), "Port must be 1024–65535.");
        if (settings.RelaunchPlaceId < 0) throw new ArgumentOutOfRangeException(nameof(settings.RelaunchPlaceId));
        string temp = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, FilePath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
