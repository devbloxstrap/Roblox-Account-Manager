using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RAM.Modern.Models;
using RAM.Modern.Services;

namespace RAM.Modern;

public partial class ToolsWindow : Window
{
    private readonly List<AccountProfile> _profiles;
    private readonly Action _profileRefresh;
    private readonly GameLibraryData _library;
    private readonly ObservableCollection<GameServer> _servers = new();
    private readonly ObservableCollection<GameEntry> _favorites = new();
    private readonly ObservableCollection<GameEntry> _recents = new();
    private string? _cursor;
    private long _currentPlaceId;
    private bool _loading;
    private CancellationTokenSource? _scanCts;
    private GameServer? _shuffledServer;
    private long _shuffledPlaceId;
    private RobloxUser? _foundPlayer;
    private bool _shuffling;
    private string? _lastJoinedJobId;
    private readonly ObservableCollection<DiscoveredGame> _recommendations = new();

    public ToolsWindow(List<AccountProfile> profiles, Action profileRefresh, long preferredPlace = 0)
    {
        InitializeComponent();
        ThemeManager.Apply(this, SettingsStore.Load());
        _profiles = profiles;
        _profileRefresh = profileRefresh;
        _library = GameLibraryStore.Load();
        ServersList.ItemsSource = _servers;
        FavoriteGamesList.ItemsSource = _favorites;
        RecentGamesList.ItemsSource = _recents;
        RecommendedGamesList.ItemsSource = _recommendations;
        UtilityAccountBox.ItemsSource = _profiles.OrderBy(p => p.Username).ToList();
        SessionAccountBox.ItemsSource = _profiles.OrderBy(p => p.Username).ToList();
        if (_profiles.Count > 0) SessionAccountBox.SelectedIndex = 0;
        if (_profiles.Count > 0) UtilityAccountBox.SelectedIndex = 0;
        Closed += (_, _) => _scanCts?.Cancel();
        if (preferredPlace > 0)
        {
            ServerPlaceBox.Text = preferredPlace.ToString();
            GamePlaceBox.Text = preferredPlace.ToString();
        }
        UpdateGames();
    }

    private void UpdateGames()
    {
        _favorites.Clear();
        foreach (var game in _library.Favorites) _favorites.Add(game);
        _recents.Clear();
        foreach (var game in _library.Recent) _recents.Add(game);
    }

    private long ReadPlaceId(TextBox box)
    {
        if (!long.TryParse(box.Text.Trim(), out long placeId) || placeId <= 0)
            throw new ArgumentException("Enter a valid positive Roblox Place ID.");
        return placeId;
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            ToolsStatusText.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "RAM tools", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task LoadServerPageAsync(string? cursor)
    {
        if (_loading) return;
        _loading = true;
        ToolsStatusText.Text = "Loading public Roblox servers…";
        try
        {
            long placeId = ReadPlaceId(ServerPlaceBox);
            var page = await RobloxApi.GetServersAsync(placeId, cursor);
            _servers.Clear();
            foreach (var item in page.Data) _servers.Add(item);
            _cursor = page.NextPageCursor;
            _currentPlaceId = placeId;
            ToolsStatusText.Text = $"Loaded {_servers.Count} public servers for place {placeId}. " +
                                   (_cursor == null ? "No further page reported." : "More pages available.");
        }
        catch (Exception ex)
        {
            _cursor = null;
            ToolsStatusText.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Server list", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _loading = false; }
    }

    private async void LoadServers_Click(object sender, RoutedEventArgs e) => await LoadServerPageAsync(null);
    private async void NextServers_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_cursor)) { ToolsStatusText.Text = "No next page available."; return; }
        await LoadServerPageAsync(_cursor);
    }

    private async void SmallServers_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        _loading = true;
        try
        {
            long place = ReadPlaceId(ServerPlaceBox);
            ToolsStatusText.Text = "Scanning up to five server pages…";
            var scan = await GameDiscovery.FindSmallServersAsync(place, 5, _scanCts.Token);
            _servers.Clear();
            foreach (var item in scan.Servers) _servers.Add(item);
            _currentPlaceId = place;
            _cursor = null;
            ToolsStatusText.Text = $"Sorted {scan.Servers.Count} non-full servers across {scan.ScannedPages} page(s)." +
                (scan.MorePagesExist ? " Further pages may exist; this is not a global search." : " Results reflect Roblox's available public listings.");
        }
        catch (OperationCanceledException) { ToolsStatusText.Text = "Server scan cancelled."; }
        catch (Exception ex) { ToolsStatusText.Text = "Server search failed: " + ex.Message; }
        finally { _loading = false; }
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => _scanCts?.Cancel();

    private void ServersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ServersList.SelectedItem is GameServer server)
            ServerDetailsText.Text = $"Players: {server.Playing}/{server.MaxPlayers}\nPing: {server.Ping?.ToString() ?? "Not available"} ms\nFPS: {server.Fps?.ToString() ?? "Not available"}\nJob ID: {server.Id}";
        else ServerDetailsText.Text = "Select a server to see details.";
    }

    private void JoinServer_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        GameServer server;
        if (ShuffleOnJoinCheck.IsChecked == true)
            server = PublicServerShuffle.Select(_servers, _lastJoinedJobId);
        else
            server = ServersList.SelectedItem as GameServer ?? throw new InvalidOperationException("Select a server, or enable shuffle-on-join.");
        _lastJoinedJobId = server.Id;
        RobloxService.OpenServer(_currentPlaceId, server.Id);
        GameLibraryStore.AddRecent(_library, _currentPlaceId);
        UpdateGames();
        ToolsStatusText.Text = "Sent server deep-link to Windows. Roblox client decides whether joining succeeds.";
    });

    private void OpenServerGame_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        long placeId = ReadPlaceId(ServerPlaceBox);
        RobloxService.OpenGame(placeId);
        GameLibraryStore.AddRecent(_library, placeId);
        UpdateGames();
        ToolsStatusText.Text = $"Opened game {placeId} through Roblox.";
    });

    private void FavoriteServerGame_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        long id = ReadPlaceId(ServerPlaceBox);
        GameLibraryStore.ToggleFavorite(_library, id, $"Game {id}");
        UpdateGames();
        ToolsStatusText.Text = "Favorite game list updated.";
    });

    private void ToggleGameFavorite_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        long placeId = ReadPlaceId(GamePlaceBox);
        GameLibraryStore.ToggleFavorite(_library, placeId, GameNameBox.Text);
        UpdateGames();
        ToolsStatusText.Text = "Favorites updated.";
    });

    private void OpenLibraryGame_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        long placeId = ReadPlaceId(GamePlaceBox);
        RobloxService.OpenGame(placeId);
        GameLibraryStore.AddRecent(_library, placeId, GameNameBox.Text);
        UpdateGames();
        ToolsStatusText.Text = "Game opened; history updated.";
    });

    private void FavoriteGame_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (FavoriteGamesList.SelectedItem is not GameEntry game) return;
        GamePlaceBox.Text = game.PlaceId.ToString();
        GameNameBox.Text = game.Name;
    }

    private void RecentGame_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (RecentGamesList.SelectedItem is not GameEntry game) return;
        GamePlaceBox.Text = game.PlaceId.ToString();
        GameNameBox.Text = game.Name;
    }

    private void ClearRecent_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (MessageBox.Show(this, "Clear recent game history?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _library.Recent.Clear();
        GameLibraryStore.Save(_library);
        UpdateGames();
        ToolsStatusText.Text = "Recent history cleared.";
    });

    private void OpenPrivate_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        RobloxService.OpenPrivateServerLink(PrivateLinkBox.Text);
        ToolsStatusText.Text = "Official Roblox link opened in your browser.";
    });

    private async void Recommend_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var id = ReadPlaceId(DiscoverPlaceBox);
            ToolsStatusText.Text = "Loading Roblox recommendations…";
            var items = await GameDiscovery.RecommendAsync(id);
            _recommendations.Clear();
            foreach (var item in items) _recommendations.Add(item);
            ToolsStatusText.Text = $"Found {items.Count} related games. Recommendations may vary by Roblox API availability.";
        }
        catch (Exception ex) { ToolsStatusText.Text = "Recommendations unavailable: " + ex.Message; }
    }

    private void OpenRecommendation_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var selected = RecommendedGamesList.SelectedItem as DiscoveredGame ?? throw new InvalidOperationException("Choose a recommended game.");
        RobloxService.OpenGame(selected.PlaceId);
        GameLibraryStore.AddRecent(_library, selected.PlaceId, selected.Name);
        UpdateGames();
    });

    private void FavoriteRecommendation_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var selected = RecommendedGamesList.SelectedItem as DiscoveredGame ?? throw new InvalidOperationException("Choose a recommended game.");
        GameLibraryStore.ToggleFavorite(_library, selected.PlaceId, selected.Name);
        UpdateGames();
        ToolsStatusText.Text = "Updated game favorites.";
    });

    private AccountProfile SelectedUtilityAccount() => UtilityAccountBox.SelectedItem as AccountProfile ??
        throw new InvalidOperationException("Select a saved account for an isolated browser window.");

    private void SelectedBrowser_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        RobloxService.OpenIsolatedAccountBrowser(SelectedUtilityAccount().Id);
        ToolsStatusText.Text = "Opened the selected account's separate Edge browser profile. Sign in manually if needed.";
    });

    private void SelectedProfile_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var p = SelectedUtilityAccount();
        if (p.UserId <= 0) throw new InvalidOperationException("Look up this account's Roblox user ID first.");
        RobloxService.OpenIsolatedAccountBrowser(p.Id, $"https://www.roblox.com/users/{p.UserId}/profile");
    });

    private void SelectedSecurity_Click(object sender, RoutedEventArgs e) => Run(() =>
        RobloxService.OpenIsolatedAccountBrowser(SelectedUtilityAccount().Id, "https://www.roblox.com/my/account#!/security"));

    private void OpenOfficial(string url)
    {
        Run(() =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            ToolsStatusText.Text = "Official Roblox page opened. Browser sessions are separate from desktop client sessions.";
        });
    }
    private void QuickLogin_Click(object sender, RoutedEventArgs e) => OpenOfficial("https://www.roblox.com/login/quick-login");
    private void AccountSettings_Click(object sender, RoutedEventArgs e) => OpenOfficial("https://www.roblox.com/my/account");
    private void Privacy_Click(object sender, RoutedEventArgs e) => OpenOfficial("https://www.roblox.com/my/account#!/privacy");
    private void Security_Click(object sender, RoutedEventArgs e) => OpenOfficial("https://www.roblox.com/my/account#!/security");
    private void Avatar_Click(object sender, RoutedEventArgs e) => OpenOfficial("https://www.roblox.com/my/avatar");
    private void Discover_Click(object sender, RoutedEventArgs e) => OpenOfficial("https://www.roblox.com/charts");
    private void OpenGroup_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (!long.TryParse(GroupIdBox.Text.Trim(), out long id) || id <= 0)
            throw new ArgumentException("Enter a valid Roblox group ID.");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"https://www.roblox.com/groups/{id}") { UseShellExecute = true });
        ToolsStatusText.Text = $"Opened Roblox group {id}.";
    });

    private async void Universe_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            long placeId = ReadPlaceId(UniversePlaceBox);
            ToolsStatusText.Text = "Fetching universe details…";
            UniverseInfoText.Text = await RobloxApi.GetUniverseInfoAsync(placeId);
            ToolsStatusText.Text = "Universe details retrieved from Roblox public endpoint.";
        }
        catch (Exception ex)
        {
            UniverseInfoText.Text = "Universe details unavailable: " + ex.Message;
            ToolsStatusText.Text = "Universe API currently unavailable for this ID.";
        }
    }

    private async void Outfits_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            long userId = ReadPlaceId(OutfitUserBox);
            ToolsStatusText.Text = "Loading publicly available outfits…";
            var outfits = await RobloxApi.GetPublicOutfitsAsync(userId);
            OutfitsList.ItemsSource = outfits;
            ToolsStatusText.Text = $"Retrieved {outfits.Count} visible outfits for User ID {userId}.";
        }
        catch (Exception ex)
        {
            ToolsStatusText.Text = "Outfits unavailable: " + ex.Message;
            OutfitsList.ItemsSource = null;
        }
    }


    private async void ShuffleServer_Click(object sender, RoutedEventArgs e)
    {
        if (_shuffling) return;
        _shuffling = true;
        try
        {
            if (!long.TryParse(ShufflePlaceBox.Text.Trim(), out long placeId) || placeId <= 0)
                throw new ArgumentException("Enter a valid game Place ID.");
            int? maxPlayers = null;
            if (!string.IsNullOrWhiteSpace(ShuffleMaxPlayersBox.Text))
            {
                if (!int.TryParse(ShuffleMaxPlayersBox.Text.Trim(), out int n) || n < 1 || n > 10000)
                    throw new ArgumentException("Maximum player filter must be between 1 and 10000.");
                maxPlayers = n;
            }
            ShuffleResultText.Text = "Scanning public servers…";
            // One scan, true server identifiers only. Never synthesize a random Job ID.
            var scanned = await GameDiscovery.FindSmallServersAsync(placeId, 5);
            _shuffledServer = PublicServerShuffle.Select(scanned.Servers, _shuffledPlaceId == placeId ? _shuffledServer?.Id : null, maxPlayers);
            _shuffledPlaceId = placeId;
            ShuffleResultText.Text = $"Selected { _shuffledServer.Playing }/{ _shuffledServer.MaxPlayers } players • Job ID: {_shuffledServer.Id} • {scanned.ScannedPages} pages checked.";
        }
        catch (Exception ex) { ShuffleResultText.Text = "Shuffle failed: " + ex.Message; }
        finally { _shuffling = false; }
    }

    private void JoinShuffled_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (_shuffledServer is null || _shuffledPlaceId <= 0) throw new InvalidOperationException("Shuffle a server first.");
        RobloxService.OpenServer(_shuffledPlaceId, _shuffledServer.Id);
        GameLibraryStore.AddRecent(_library, _shuffledPlaceId);
        UpdateGames();
        ToolsStatusText.Text = "Opened official server deep-link. Roblox must confirm the join.";
    });

    private void CopyShuffled_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (_shuffledServer is null) throw new InvalidOperationException("Shuffle a server first.");
        Clipboard.SetText(_shuffledServer.Id);
        ToolsStatusText.Text = "Public Job ID copied (not an authentication link).";
    });

    private async void FindPlayer_Click(object sender, RoutedEventArgs e)
    {
        _foundPlayer = null;
        try
        {
            PlayerResultText.Text = "Looking up public user record…";
            var player = await RobloxApi.LookupUsernameAsync(PlayerUsernameBox.Text.Trim());
            if (player is null) { PlayerResultText.Text = "No matching public Roblox username was found."; return; }
            _foundPlayer = player;
            PlayerResultText.Text = $"@{player.Name} • {player.DisplayName} • User ID {player.Id}\nPublic account found; in-game presence not disclosed.";
        }
        catch (Exception ex) { PlayerResultText.Text = "Lookup unavailable: " + ex.Message; }
    }

    private void OpenFoundProfile_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (_foundPlayer is null) throw new InvalidOperationException("Look up a username first.");
        RobloxService.OpenProfile(_foundPlayer.Id);
    });

    private void AuditSessions_Click(object sender, RoutedEventArgs e)
    {
        var counts = SessionInventory.Count(_profiles);
        HealthResultText.Text = $"{counts.WithSnapshot} saved encrypted snapshots • {counts.WithoutSnapshot} profiles without snapshots. Files may be expired; verify the current client separately.";
    }

    private async void HealthCheck_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            HealthResultText.Text = "Checking installed Roblox desktop session…";
            var current = await RobloxSessionIdentity.CheckCurrentAsync();
            HealthResultText.Text = current is null ? "No active desktop session verified. Sign in with official Roblox client." :
                $"Verified active desktop session for @{current.Name} (ID {current.Id}).";
        }
        catch (Exception ex) { HealthResultText.Text = "Could not verify desktop session: " + ex.Message; }
    }

    private AccountProfile SelectedSessionProfile() => SessionAccountBox.SelectedItem as AccountProfile
        ?? throw new InvalidOperationException("Select a saved profile first.");

    private void OpenSafeLogin_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        RobloxService.OpenIsolatedAccountBrowser(SelectedSessionProfile().Id);
        HealthResultText.Text = "Opened official Roblox login using a separate browser profile.";
    });

    private void ManageBrowserPasswords_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        RobloxService.OpenIsolatedAccountPasswordManager(SelectedSessionProfile().Id);
        HealthResultText.Text = "Opened Edge Password Manager. RAM does not access passwords.";
    });

    private void Export_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var picker = new SaveFileDialog { Filter = "JSON|*.json", FileName = "ram-profiles-public.json", AddExtension = true };
        if (picker.ShowDialog(this) != true) return;
        var safe = _profiles.Select(p => new ExportProfile(p.Username, p.UserId, p.DisplayName, p.Group, p.Note, p.Favorite, p.PreferredPlaceId));
        File.WriteAllText(picker.FileName, JsonSerializer.Serialize(safe, new JsonSerializerOptions { WriteIndented = true }));
        ToolsStatusText.Text = $"Exported {_profiles.Count} profiles without cookies, passwords, or sessions.";
    });

    private void Import_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var picker = new OpenFileDialog { Filter = "JSON|*.json" };
        if (picker.ShowDialog(this) != true) return;
        if (new FileInfo(picker.FileName).Length > 2_000_000) throw new IOException("Import file is too large.");
        var imported = JsonSerializer.Deserialize<List<ExportProfile>>(File.ReadAllText(picker.FileName),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ExportProfile>();
        int added = MergeProfiles(imported);
        ToolsStatusText.Text = $"Imported {added} new metadata profiles; existing sessions unchanged.";
    });

    private int MergeProfiles(IEnumerable<ExportProfile> entries)
    {
        int added = 0;
        foreach (var item in entries.Take(5000))
        {
            string username = (item.Username ?? "").Trim().TrimStart('@');
            if (username.Length is < 3 or > 20 || username.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch == '_')))
                continue;
            if (_profiles.Any(p => p.Username.Equals(username, StringComparison.OrdinalIgnoreCase) ||
                                   (item.UserId > 0 && p.UserId == item.UserId))) continue;
            _profiles.Add(new AccountProfile
            {
                Username = username, DisplayName = item.DisplayName ?? "", UserId = Math.Max(0, item.UserId),
                Group = string.IsNullOrWhiteSpace(item.Group) ? "General" : item.Group,
                Note = item.Note ?? "", Favorite = item.Favorite,
                PreferredPlaceId = Math.Max(0, item.PreferredPlaceId)
            });
            added++;
        }
        if (added != 0) { ProfileStore.Save(_profiles); _profileRefresh(); }
        return added;
    }

    private void ImportCsv_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var picker = new OpenFileDialog { Filter = "CSV|*.csv" };
        if (picker.ShowDialog(this) != true) return;
        if (new FileInfo(picker.FileName).Length > 2_000_000) throw new IOException("Import file is too large.");
        var lines = File.ReadAllLines(picker.FileName).Where(s => !string.IsNullOrWhiteSpace(s)).Skip(1).Take(5000);
        var entries = new List<ExportProfile>();
        foreach (var line in lines)
        {
            // Deliberately only simple comma-delimited metadata; never parse passwords or cookies.
            var cols = line.Split(',', 5);
            if (cols.Length < 2 || !long.TryParse(cols[1].Trim(), out var id)) continue;
            entries.Add(new ExportProfile(cols[0], id, cols.Length > 2 ? cols[2] : "",
                cols.Length > 3 ? cols[3] : "General", cols.Length > 4 ? cols[4] : "", false, 0));
        }
        int added = MergeProfiles(entries);
        ToolsStatusText.Text = $"Imported {added} new profiles from CSV. Simple CSV only (no quoted commas).";
    });

    private void OpenData_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        Directory.CreateDirectory(ProfileStore.Folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ProfileStore.Folder) { UseShellExecute = true });
    });
}
