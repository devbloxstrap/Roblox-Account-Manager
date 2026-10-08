using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

/// <summary>Portable encrypted backup of non-secret profile metadata only.
/// Session cookies and browser data are deliberately excluded.</summary>
public static class EncryptedProfileBackup
{
    private static readonly byte[] Signature = Encoding.ASCII.GetBytes("RAMMETA1");
    private const int SaltLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int Iterations = 310_000;
    private const int MaxPayloadBytes = 16 * 1024 * 1024;

    private sealed record TransferProfile(string Username, string DisplayName, long UserId, string Group, string Note, bool Favorite, long PreferredPlaceId, string PreferredJobId, int SortOrder);

    public static void Export(string path, string password, IEnumerable<AccountProfile> profiles)
    {
        if (password.Length < 12) throw new ArgumentException("Backup password must contain at least 12 characters.");
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(profiles.Select(p => new TransferProfile(
            p.Username, p.DisplayName, p.UserId, p.Group, p.Note, p.Favorite, p.PreferredPlaceId, p.PreferredJobId, p.SortOrder)).ToList());
        if (plain.Length > MaxPayloadBytes) throw new InvalidDataException("Profile metadata is too large to back up.");
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var tag = new byte[TagLength];
        var cipher = new byte[plain.Length];
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        try
        {
            using (var aes = new AesGcm(key, TagLength)) aes.Encrypt(nonce, plain, cipher, tag, Signature);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string tmp = path + ".tmp";
            try
            {
                using (var fs = File.Open(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(Signature);
                    fs.Write(salt);
                    fs.Write(nonce);
                    fs.Write(tag);
                    fs.Write(cipher);
                    fs.Flush(true);
                }
                File.Move(tmp, path, true);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static List<AccountProfile> Import(string path, string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Enter the backup password.");
        var file = new FileInfo(path);
        if (!file.Exists || file.Length < Signature.Length + SaltLength + NonceLength + TagLength || file.Length > MaxPayloadBytes + 1024)
            throw new InvalidDataException("Backup file is missing or malformed.");
        byte[] payload = File.ReadAllBytes(path);
        int offset = 0;
        if (!payload.AsSpan(0, Signature.Length).SequenceEqual(Signature)) throw new InvalidDataException("Not a RAMMETA1 encrypted metadata backup.");
        offset += Signature.Length;
        var salt = payload.AsSpan(offset, SaltLength); offset += SaltLength;
        var nonce = payload.AsSpan(offset, NonceLength); offset += NonceLength;
        var tag = payload.AsSpan(offset, TagLength); offset += TagLength;
        var cipher = payload.AsSpan(offset);
        byte[] plain = new byte[cipher.Length];
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        try
        {
            try { using var aes = new AesGcm(key, TagLength); aes.Decrypt(nonce, cipher, tag, plain, Signature); }
            catch (CryptographicException) { throw new InvalidDataException("Wrong backup password or corrupted encrypted backup."); }
            var imported = JsonSerializer.Deserialize<List<TransferProfile>>(plain) ?? new();
            if (imported.Count > 5000) throw new InvalidDataException("Too many imported profiles.");
            return imported.Where(p => !string.IsNullOrWhiteSpace(p.Username))
                .Select(p => new AccountProfile
                {
                    Username = p.Username.Trim().TrimStart('@'), DisplayName = p.DisplayName ?? "",
                    UserId = Math.Max(0, p.UserId), Group = string.IsNullOrWhiteSpace(p.Group) ? "General" : p.Group,
                    Note = p.Note ?? "", Favorite = p.Favorite, PreferredPlaceId = Math.Max(0, p.PreferredPlaceId),
                    PreferredJobId = p.PreferredJobId ?? "", SortOrder = p.SortOrder
                }).ToList();
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
}
