using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RAM.Modern.Services;

/// <summary>Per-profile login secrets, encrypted to the current Windows user. Never exports secrets.</summary>
public static class AccountAuthStore
{
    private static readonly byte[] Header = "RAMAUTH1"u8.ToArray();
    private static readonly byte[] Entropy = "RAM.Modern.AccountAuth.v1"u8.ToArray();
    private static string Folder => Path.Combine(ProfileStore.Folder, "account-auth");
    private static string FileFor(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A saved account profile is required.");
        return Path.Combine(Folder, id.ToString("N") + ".authdpapi");
    }
    public static bool HasLogin(Guid id) => id != Guid.Empty && File.Exists(FileFor(id));

    public static void Save(Guid id, string securityCookie)
    {
        if (string.IsNullOrWhiteSpace(securityCookie) || securityCookie.Length > 8192 ||
            securityCookie.Any(ch => ch is '\r' or '\n' or ';'))
            throw new ArgumentException("Invalid account session. Nothing was saved.");
        Directory.CreateDirectory(Folder);
        byte[] plain = Encoding.UTF8.GetBytes(securityCookie);
        try
        {
            byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            byte[] payload = new byte[Header.Length + encrypted.Length];
            Header.CopyTo(payload, 0);
            encrypted.CopyTo(payload, Header.Length);
            string target = FileFor(id);
            string tmp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(payload); stream.Flush(true); }
                File.Move(tmp, target, true);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public static string Load(Guid id)
    {
        string path = FileFor(id);
        if (!File.Exists(path)) throw new FileNotFoundException("No account-specific login found. Use Add via browser to sign in to this account.");
        byte[] blob = File.ReadAllBytes(path);
        if (blob.Length <= Header.Length || blob.Length > 32_000 || !blob.AsSpan(0, Header.Length).SequenceEqual(Header))
            throw new InvalidDataException("Encrypted account session is corrupt. Re-login to this account.");
        byte[] decrypted = ProtectedData.Unprotect(blob.AsSpan(Header.Length).ToArray(), Entropy, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(decrypted); }
        finally { CryptographicOperations.ZeroMemory(decrypted); }
    }
    public static void Delete(Guid id)
    {
        if (id == Guid.Empty) return;
        string path = FileFor(id);
        if (File.Exists(path)) File.Delete(path);
    }
}
