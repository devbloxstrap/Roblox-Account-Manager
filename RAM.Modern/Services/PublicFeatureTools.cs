using System.Security.Cryptography;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

/// <summary>Selection based solely on public Roblox server listings, not generated or guessed JobIds.</summary>
public static class PublicServerShuffle
{
    public static GameServer Select(IEnumerable<GameServer> candidates, string? previousId = null, int? maxPlayers = null)
    {
        if (maxPlayers is <= 0) throw new ArgumentOutOfRangeException(nameof(maxPlayers));
        var available = candidates.Where(s => Guid.TryParse(s.Id, out _) && s.MaxPlayers > 0 &&
             s.Playing >= 0 && s.Playing < s.MaxPlayers &&
             (!maxPlayers.HasValue || s.Playing <= maxPlayers.Value)).GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
             .Select(g => g.First()).ToList();
        if (available.Count == 0) throw new InvalidOperationException("No joinable public servers matched the filter. Try a larger player limit or rescan.");
        if (available.Count > 1 && !string.IsNullOrEmpty(previousId))
            available.RemoveAll(s => string.Equals(s.Id, previousId, StringComparison.OrdinalIgnoreCase));
        return available[RandomNumberGenerator.GetInt32(available.Count)];
    }
}

/// <summary>Public-user lookup; intentionally never infers hidden in-game presence or location.</summary>
public static class PublicPlayerLookup
{
    public static string ProfileUrl(long userId)
    {
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
        return $"https://www.roblox.com/users/{userId}/profile";
    }

    public static bool HasVerifiedIdentity(AccountProfile profile, RobloxUser actual) =>
        profile.UserId > 0 && profile.UserId == actual.Id &&
        profile.Username.Equals(actual.Name, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Human-readable session state, never reads/export authentication secrets.</summary>
public static class SessionInventory
{
    public static (int WithSnapshot, int WithoutSnapshot) Count(IEnumerable<AccountProfile> profiles)
    {
        int yes = 0, no = 0;
        foreach (var profile in profiles)
            if (SessionSwitcher.HasSnapshot(profile.Id)) yes++; else no++;
        return (yes, no);
    }
}
