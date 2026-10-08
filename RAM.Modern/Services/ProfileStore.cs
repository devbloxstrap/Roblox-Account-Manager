using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

/// <summary>Current-user DPAPI encrypted account profile database.
/// Migrates legacy profiles.json only after a verified encrypted write.</summary>
public static class ProfileStore
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RAM.Modern");
    public static string Pathname => Path.Combine(Folder, "profiles.ramdpapi");
    private static string LegacyPath => Path.Combine(Folder, "profiles.json");
    private static string BackupPath => Pathname + ".bak";
    private static readonly byte[] Header = Encoding.ASCII.GetBytes("RAMDPAPI1");
    private static readonly byte[] Entropy = Encoding.ASCII.GetBytes("RAM.Modern.ProfileStore.v1");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private const int MaxBytes = 16 * 1024 * 1024;

    public static List<AccountProfile> Load()
    {
        if (File.Exists(Pathname))
        {
            try { return DecryptFromFile(Pathname); }
            catch (Exception error)
            {
                if (File.Exists(BackupPath))
                {
                    try
                    {
                        var restored = DecryptFromFile(BackupPath);
                        // The backup is verified. Restore the working database before allowing any future saves.
                        File.Copy(BackupPath, Pathname, true);
                        return restored;
                    }
                    catch { /* primary error is more useful */ }
                }
                throw new IOException("Cannot decrypt profile database. Original file was preserved at " + Pathname, error);
            }
        }
        if (!File.Exists(LegacyPath)) return new();
        try
        {
            var file = new FileInfo(LegacyPath);
            if (file.Length > MaxBytes) throw new InvalidDataException("Legacy profile database is too large.");
            var list = JsonSerializer.Deserialize<List<AccountProfile>>(File.ReadAllText(LegacyPath), Options) ?? new();
            Save(list); // Create and verify encrypted storage before deleting plaintext.
            if (File.Exists(LegacyPath)) File.Delete(LegacyPath);
            if (File.Exists(LegacyPath + ".bak")) File.Delete(LegacyPath + ".bak");
            return list;
        }
        catch (Exception error)
        {
            throw new IOException("Legacy profiles remain at " + LegacyPath + ". Automatic encrypted migration did not complete.", error);
        }
    }

    private static List<AccountProfile> DecryptFromFile(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length <= Header.Length || data.Length > MaxBytes + 512 || !data.AsSpan(0, Header.Length).SequenceEqual(Header))
            throw new InvalidDataException("Invalid encrypted profile database.");
        byte[] plain = ProtectedData.Unprotect(data.AsSpan(Header.Length).ToArray(), Entropy, DataProtectionScope.CurrentUser);
        try
        {
            return JsonSerializer.Deserialize<List<AccountProfile>>(plain, Options) ?? new();
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public static void Save(IEnumerable<AccountProfile> items)
    {
        Directory.CreateDirectory(Folder);
        // Fail closed: a failed initial read must never silently overwrite the user's only
        // encrypted database. Recovery happens in Load() from the validated backup instead.
        if (File.Exists(Pathname)) _ = DecryptFromFile(Pathname);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(items, Options);
        try
        {
            if (plain.Length > MaxBytes) throw new InvalidDataException("Profile data is too large.");
            byte[] protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            byte[] payload = new byte[Header.Length + protectedBytes.Length];
            Header.CopyTo(payload, 0);
            protectedBytes.CopyTo(payload, Header.Length);
            string tmp = Pathname + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(payload); stream.Flush(true); }
                _ = DecryptFromFile(tmp); // Verify before replacing user data.
                if (File.Exists(Pathname)) File.Copy(Pathname, BackupPath, true);
                File.Move(tmp, Pathname, true);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
}
