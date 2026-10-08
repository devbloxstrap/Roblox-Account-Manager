using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace RAM.Modern.Services;

/// <summary>Encrypted Windows-user-scoped backups of the local Roblox login file.
/// A restored file is NOT proof of Roblox authentication; Roblox can reject the session.</summary>
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
                throw new InvalidOperationException("Close every Roblox Player and Studio window before saving or restoring a session.");
    }
    private static byte[] ReadValidated(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Roblox local login file was not found. Sign in through the installed Roblox desktop client first.", path);
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0 || bytes.Length > 1024 * 1024 || Array.IndexOf(bytes, (byte)0) >= 0)
            throw new InvalidDataException("Login file is empty, damaged, or unusually large. No changes were made.");
        return bytes;
    }
    private static void AtomicSave(string target, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
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
        try
        {
            var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            AtomicSave(Snapshot(id), encrypted);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public static bool Restore(Guid id)
    {
        RequireStopped();
        if (!HasSnapshot(id)) throw new FileNotFoundException("No saved session exists for this profile. Log in with the desktop client and save its session first.");
        var encrypted = File.ReadAllBytes(Snapshot(id));
        var bytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        try
        {
            if (bytes.Length == 0 || bytes.Length > 1024 * 1024 || Array.IndexOf(bytes, (byte)0) >= 0)
                throw new InvalidDataException("Saved login snapshot is invalid.");
            if (File.Exists(LivePath))
            {
                var previous = ReadValidated(LivePath);
                try { AtomicSave(Path.Combine(Vault, "previous-session.dpapi"), ProtectedData.Protect(previous, Entropy, DataProtectionScope.CurrentUser)); }
                finally { CryptographicOperations.ZeroMemory(previous); }
            }
            AtomicSave(LivePath, bytes);
            // This verifies only that the bytes were restored, not that Roblox accepted them.
            var roundtrip = File.ReadAllBytes(LivePath);
            try { return CryptographicOperations.FixedTimeEquals(bytes, roundtrip); }
            finally { CryptographicOperations.ZeroMemory(roundtrip); }
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public static void Delete(Guid id) { var path = Snapshot(id); if (File.Exists(path)) File.Delete(path); }
}
