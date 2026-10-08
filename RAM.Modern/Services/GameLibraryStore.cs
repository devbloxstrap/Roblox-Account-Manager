using System.IO;
using System.Text.Json;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

public static class GameLibraryStore
{
    public static string Pathname => Path.Combine(ProfileStore.Folder, "games.json");
    public static GameLibraryData Load()
    {
        if (!File.Exists(Pathname)) return new GameLibraryData();
        try { return JsonSerializer.Deserialize<GameLibraryData>(File.ReadAllText(Pathname)) ?? new GameLibraryData(); }
        catch (JsonException ex) { throw new IOException("Game library is damaged and has not been overwritten: " + Pathname, ex); }
    }
    public static void Save(GameLibraryData data)
    {
        Directory.CreateDirectory(ProfileStore.Folder);
        string temp = Pathname + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(Pathname)) File.Copy(Pathname, Pathname + ".bak", true);
            File.Move(temp, Pathname, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void AddRecent(GameLibraryData data, long placeId, string? name = null)
    {
        if (placeId <= 0) throw new ArgumentException("Invalid Place ID.");
        var existing = data.Recent.FirstOrDefault(x => x.PlaceId == placeId);
        name = string.IsNullOrWhiteSpace(name) ? existing?.Name ?? $"Game {placeId}" : name.Trim();
        data.Recent.RemoveAll(x => x.PlaceId == placeId);
        data.Recent.Insert(0, new GameEntry { PlaceId = placeId, Name = name, LastOpenedUtc = DateTimeOffset.UtcNow });
        if (data.Recent.Count > 50) data.Recent.RemoveRange(50, data.Recent.Count - 50);
        Save(data);
    }

    public static void ToggleFavorite(GameLibraryData data, long placeId, string name)
    {
        var existing = data.Favorites.FirstOrDefault(x => x.PlaceId == placeId);
        if (existing != null) data.Favorites.Remove(existing);
        else data.Favorites.Insert(0, new GameEntry { PlaceId = placeId, Name = string.IsNullOrWhiteSpace(name) ? $"Game {placeId}" : name.Trim() });
        Save(data);
    }
}
