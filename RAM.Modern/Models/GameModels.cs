using System.Text.Json.Serialization;

namespace RAM.Modern.Models;

public sealed class GameEntry
{
    public long PlaceId { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset LastOpenedUtc { get; set; } = DateTimeOffset.UtcNow;
    public override string ToString() => $"{Name}  •  Place {PlaceId}";
}

public sealed class GameLibraryData
{
    public List<GameEntry> Favorites { get; set; } = new();
    public List<GameEntry> Recent { get; set; } = new();
}

public sealed class GameServer
{
    public string Id { get; set; } = "";
    public int Playing { get; set; }
    public int MaxPlayers { get; set; }
    public double? Ping { get; set; }
    public double? Fps { get; set; }
    [JsonIgnore] public string Summary => $"{Playing}/{MaxPlayers} players  •  {((Ping.HasValue) ? Ping.Value + " ms" : "Ping n/a")}  •  {Id[..Math.Min(8, Id.Length)]}…";
    public override string ToString() => Summary;
}

public sealed class ServerPage
{
    public List<GameServer> Data { get; set; } = new();
    public string? NextPageCursor { get; set; }
    public string? PreviousPageCursor { get; set; }
}

public sealed class RobloxUser
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
}

public sealed record ExportProfile(string Username, long UserId, string DisplayName, string Group, string Note, bool Favorite, long PreferredPlaceId);
