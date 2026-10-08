using System.IO;
using System.Xml.Linq;

namespace RAM.Modern.Services;

/// <summary>Best-effort edits to the existing Roblox GUI framerate setting,
/// without inserting unsupported FastFlags or exceeding 240 FPS.</summary>
public static class FpsSettings
{
    public static string SettingsFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "GlobalBasicSettings_13.xml");
    public static string BackupFile => SettingsFile + ".ram-backup";
    private static readonly int[] Supported = { 30, 60, 90, 120, 144, 165, 240 };

    private static XElement GetFpsNode(XDocument document) =>
        document.Descendants().FirstOrDefault(x => x.Name.LocalName.Equals("int", StringComparison.OrdinalIgnoreCase) &&
            string.Equals((string?)x.Attribute("name"), "FramerateCap", StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidDataException("Roblox's FramerateCap setting was not found. Use Roblox's own settings menu instead.");

    public static int? Read()
    {
        if (!File.Exists(SettingsFile)) return null;
        var doc = XDocument.Load(SettingsFile);
        return int.TryParse(GetFpsNode(doc).Value, out var cap) ? cap : null;
    }

    public static void Apply(int fps)
    {
        if (!Supported.Contains(fps)) throw new ArgumentOutOfRangeException(nameof(fps), "Choose a supported preset from 30 to 240 FPS.");
        RequireStopped();
        if (!File.Exists(SettingsFile)) throw new FileNotFoundException("Roblox settings XML was not found; launch Roblox and set framerate in its client first.", SettingsFile);
        var xml = XDocument.Load(SettingsFile, LoadOptions.PreserveWhitespace);
        var node = GetFpsNode(xml);
        if (!File.Exists(BackupFile)) File.Copy(SettingsFile, BackupFile);
        node.Value = fps.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string tmp = SettingsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            xml.Save(tmp, SaveOptions.DisableFormatting);
            if (!int.TryParse(GetFpsNode(XDocument.Load(tmp)).Value, out int check) || check != fps)
                throw new InvalidDataException("FPS configuration validation failed.");
            File.Move(tmp, SettingsFile, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public static void RestoreBackup()
    {
        RequireStopped();
        if (!File.Exists(BackupFile)) throw new FileNotFoundException("No original FPS settings backup exists.");
        _ = XDocument.Load(BackupFile);
        File.Copy(BackupFile, SettingsFile, true);
    }

    private static void RequireStopped()
    {
        if (RobloxService.RunningPlayerCount() > 0) throw new InvalidOperationException("Close Roblox Player before editing FPS settings.");
    }
}
