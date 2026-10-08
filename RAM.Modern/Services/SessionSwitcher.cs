using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;

namespace RAM.Modern.Services;

/// <summary>Windows-only, per-user protected snapshots of Roblox's login file.
/// Session restore is best-effort; Roblox may reject expired or revoked sessions.</summary>
public static class SessionSwitcher
{
    public static string LivePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "LocalStorage", "RobloxCookies.dat");
    private static string Vault => Path.Combine(ProfileStore.Folder, "sessions");
    private static string Snapshot(Guid id) => Path.Combine(Vault, id.ToString("N") + ".dpapi");
    private static readonly byte[] Entropy = "RAM.Modern.Session.v1"u8.ToArray();
    public static bool HasSnapshot(Guid id) => File.Exists(Snapshot(id));

    private static void RequireStopped()
    {
        foreach (var name in new[] { "RobloxPlayerBeta", "RobloxPlayer", "RobloxStudioBeta", "Roblox" })
            if (Process.GetProcessesByName(name).Length > 0)
                throw new InvalidOperationException("Close every Roblox client and Roblox Studio window before saving or restoring a session.");
    }
    private static byte[] ReadValidated(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Roblox login file not found. Sign in using the installed Roblox client first.", path);
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0 || bytes.Length > 1024 * 1024 || Array.IndexOf(bytes, (byte)0) >= 0)
            throw new InvalidDataException("Login file is empty, corrupt or unusually large. No changes were made.");
        return bytes;
    }
    private static void AtomicSave(string target, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static void SaveCurrent(Guid id)
    {
        RequireStopped();
        var bytes = ReadValidated(LivePath);
        var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        CryptographicOperations.ZeroMemory(bytes);
        AtomicSave(Snapshot(id), encrypted);
    }
    public static void Restore(Guid id)
    {
        RequireStopped();
        if (!HasSnapshot(id)) throw new FileNotFoundException("No saved session for this profile. Sign in first, then choose Save current session.");
        var encrypted = File.ReadAllBytes(Snapshot(id));
        var bytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        try
        {
            if (bytes.Length == 0 || bytes.Length > 1024 * 1024 || Array.IndexOf(bytes, (byte)0) >= 0)
                throw new InvalidDataException("Saved login snapshot is invalid.");
            // Preserve the previous live session; protect the backup with DPAPI too.
            if (File.Exists(LivePath))
            {
                var previous = ReadValidated(LivePath);
                try { AtomicSave(Path.Combine(Vault, "previous-session.dpapi"), ProtectedData.Protect(previous, Entropy, DataProtectionScope.CurrentUser)); }
                finally { CryptographicOperations.ZeroMemory(previous); }
            }
            AtomicSave(LivePath, bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public static void Delete(Guid id) { var path=Snapshot(id); if (File.Exists(path)) File.Delete(path); }
}
