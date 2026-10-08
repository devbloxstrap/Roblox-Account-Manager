using RAM.Modern.Models;
using RAM.Modern.Services;
using System.Security.Cryptography;

int success = 0, failures = 0;
void Test(string name, Action test)
{
    try { test(); success++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e); }
}
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (Exception ex) when (ex is T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}

var a = new AccountProfile { Username = "A_User", UserId = 11, Group = "010 Main", Favorite = true, PreferredPlaceId = 920 };
var b = new AccountProfile { Username = "B_User", UserId = 22, Group = "002 Storage" };
Test("Numeric group priority", () =>
{
    Check(GroupSort.Parse("025 My Group").Priority == 25, "Parse priority");
    Check(GroupSort.Parse("025 My Group").DisplayName == "My Group", "Display name");
    Check(GroupSort.Sort(new[] { a, b }).First() == b, "Sorting");
});
Test("Public player identity", () =>
{
    Check(PublicPlayerLookup.HasVerifiedIdentity(a, new RobloxUser { Id = 11, Name = "a_user" }), "Expected match");
    Check(!PublicPlayerLookup.HasVerifiedIdentity(a, new RobloxUser { Id = 12, Name = "a_user" }), "Wrong ID accepted");
    Check(PublicPlayerLookup.ProfileUrl(42).EndsWith("/42/profile"), "Official profile URL");
    Throws<ArgumentOutOfRangeException>(() => PublicPlayerLookup.ProfileUrl(0));
});
Test("Server shuffle refuses invented or full identifiers", () =>
{
    var g1 = Guid.NewGuid().ToString();
    var g2 = Guid.NewGuid().ToString();
    var choices = new[] {
        new GameServer { Id = g1, Playing = 2, MaxPlayers = 20 },
        new GameServer { Id = g2, Playing = 10, MaxPlayers = 20 },
        new GameServer { Id = "not-a-real-server-id", Playing = 0, MaxPlayers = 20 },
        new GameServer { Id = Guid.NewGuid().ToString(), Playing = 20, MaxPlayers = 20 }
    };
    Check(PublicServerShuffle.Select(choices, g1).Id == g2, "Should avoid prior selection");
    Check(PublicServerShuffle.Select(choices, maxPlayers: 5).Id == g1, "Player filter");
    Throws<InvalidOperationException>(() => PublicServerShuffle.Select(choices, maxPlayers: 1));
    Throws<ArgumentOutOfRangeException>(() => PublicServerShuffle.Select(choices, maxPlayers: -1));
});
Test("Safe session inventory", () =>
{
    var profiles = new[] { new AccountProfile { Id = Guid.NewGuid(), Username = "Test" } };
    var counts = SessionInventory.Count(profiles);
    Check(counts.WithSnapshot == 0 && counts.WithoutSnapshot == 1, "Unexpected session file");
});
Test("Password-encrypted metadata roundtrip, wrong password and tamper", () =>
{
    var path = Path.Combine(Path.GetTempPath(), "ram-test-" + Guid.NewGuid().ToString("N") + ".rammeta");
    try
    {
        EncryptedProfileBackup.Export(path, "Test-Password-With-16-Characters!", new[] { a, b });
        var text = Convert.ToHexString(File.ReadAllBytes(path));
        Check(!text.Contains(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes("A_User"))), "Plaintext leaked");
        var restore = EncryptedProfileBackup.Import(path, "Test-Password-With-16-Characters!");
        Check(restore.Count == 2 && restore[0].PreferredPlaceId == 920, "Import mismatch");
        Throws<InvalidDataException>(() => EncryptedProfileBackup.Import(path, "incorrect-password"));
        var bytes = File.ReadAllBytes(path);
        bytes[^1] ^= 0x7f;
        File.WriteAllBytes(path, bytes);
        Throws<InvalidDataException>(() => EncryptedProfileBackup.Import(path, "Test-Password-With-16-Characters!"));
    }
    finally { if (File.Exists(path)) File.Delete(path); }
});
Test("Corrupt database is not overwritten by Save", () =>
{
    var file = ProfileStore.Pathname;
    var original = File.Exists(file) ? File.ReadAllBytes(file) : null;
    try
    {
        Directory.CreateDirectory(ProfileStore.Folder);
        File.WriteAllBytes(file, new byte[] { 10, 20, 30, 40 });
        Throws<InvalidDataException>(() => ProfileStore.Save(new[] { a }));
        Check(File.ReadAllBytes(file).SequenceEqual(new byte[] { 10, 20, 30, 40 }), "Corrupt original overwritten");
    }
    finally { if (original == null) File.Delete(file); else File.WriteAllBytes(file, original); }
});
Test("Roblox URL and FPS input safeguards", () =>
{
    Throws<ArgumentException>(() => RobloxService.OpenServer(1, "not-guid"));
    Throws<ArgumentException>(() => RobloxService.OpenPrivateServerLink("https://evil.example/login"));
    Throws<ArgumentOutOfRangeException>(() => FpsSettings.Apply(999));
});
Test("Current-user encrypted profile database roundtrip", () =>
{
    // CI runner profile is ephemeral. Preserve and restore any prior files even if this test fails.
    var file = ProfileStore.Pathname;
    var original = File.Exists(file) ? File.ReadAllBytes(file) : null;
    var backup = file + ".bak";
    var priorBackup = File.Exists(backup) ? File.ReadAllBytes(backup) : null;
    try
    {
        var entry = new AccountProfile { Username = "RamSmokeTest", UserId = 90123, Group = "050 Test" };
        ProfileStore.Save(new[] { entry });
        Check(ProfileStore.Load().Count == 1 && ProfileStore.Load()[0].UserId == 90123, "Readback mismatch");
        Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(file)).Contains("RamSmokeTest"), "Unencrypted profile leaked");
    }
    finally
    {
        if (original == null) File.Delete(file); else File.WriteAllBytes(file, original);
        if (priorBackup == null) File.Delete(backup); else File.WriteAllBytes(backup, priorBackup);
    }
});
Console.WriteLine($"RESULT: {success} PASS, {failures} FAIL");
return failures == 0 ? 0 : 1;
