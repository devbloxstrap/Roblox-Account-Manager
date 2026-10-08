using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAM.Modern.Models;
using RAM.Modern.Services;

namespace RAM.Modern;

public partial class MainWindow : Window
{
    private readonly List<AccountProfile> _profiles;
    private readonly ObservableCollection<AccountProfile> _displayed = new();
    private Guid? _selectedId;
    private bool _sessionBusy;
    private readonly AdvancedSettings _advancedSettings;
    private readonly RobloxWatcher _watcher;
    private readonly LocalDeveloperApi _localApi;

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            _profiles = ProfileStore.Load();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Profile data error", MessageBoxButton.OK, MessageBoxImage.Warning);
            _profiles = new();
        }

        AccountsList.ItemsSource = _displayed;
        GroupBox.Text = "General";
        Refresh();
        UpdateWindowVisualState();
        _ = LoadAvatarsAsync();
        try { _advancedSettings = SettingsStore.Load(); }
        catch (Exception ex)
        {
            _advancedSettings = new AdvancedSettings();
            MessageBox.Show(ex.Message, "Advanced settings could not be loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        ThemeManager.Apply(this, _advancedSettings);
        _watcher = new RobloxWatcher(() => _advancedSettings);
        _watcher.StatusChanged += status =>
        {
            if (_advancedSettings.WatcherNotifications)
                Dispatcher.BeginInvoke(new Action(() => StatusText.Text = status));
        };
        _localApi = new LocalDeveloperApi(() => Dispatcher.Invoke(() => (IReadOnlyList<AccountProfile>)_profiles.ToArray()), () => RobloxService.RunningPlayerCount() > 0);
        _localApi.StatusChanged += status => Dispatcher.BeginInvoke(new Action(() => StatusText.Text = status));
        if (_advancedSettings.LocalApiEnabled)
        {
            try { _localApi.Start(_advancedSettings.LocalApiPort); }
            catch (Exception ex) { StatusText.Text = "Local API not started: " + ex.Message; }
        }
        Closed += (_, _) => { _watcher.Dispose(); _localApi.Dispose(); };
    }

    private void Refresh()
    {
        string q = SearchBox?.Text?.Trim() ?? string.Empty;
        _displayed.Clear();

        IEnumerable<AccountProfile> query = _profiles;
        if (q.Length > 0)
        {
            query = query.Where(p =>
                ($"{p.Username} {p.DisplayName} {p.Group} {p.Note} {p.UserId}")
                    .Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        int sortMode = SortBox?.SelectedIndex ?? 0;
        query = sortMode switch
        {
            1 => query.OrderByDescending(p => p.LastSelectedUtc ?? DateTimeOffset.MinValue),
            2 => query.OrderBy(p => p.Username, StringComparer.OrdinalIgnoreCase),
            3 => query.OrderBy(p => p.SortOrder).ThenBy(p => p.Username),
            _ => GroupSort.Sort(query)
        };
        foreach (var p in query)
        {
            _displayed.Add(p);
        }

        if (_selectedId is Guid id)
            AccountsList.SelectedItem = _displayed.FirstOrDefault(x => x.Id == id);

        TopSummaryText.Text = $"{_profiles.Count} profile{(_profiles.Count == 1 ? string.Empty : "s")} stored locally";
        EmptyAccountsState.Visibility = _displayed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var selected = _profiles.FirstOrDefault(x => x.Id == _selectedId);
        UpdateSelectionSummary(selected);

        StatusText.Text = _profiles.Count == 0
            ? "Ready. Create your first profile to begin. Account logins can be added independently via the official browser."
            : $"Ready. {_profiles.Count} profile{(_profiles.Count == 1 ? string.Empty : "s")} available. Account logins are stored encrypted for this Windows user.";
    }

    private void UpdateSelectionSummary(AccountProfile? p)
    {
        if (p is null)
        {
            SelectedTitleText.Text = "No profile selected";
            SelectedSubtitleText.Text = "Create or select a profile to begin.";
            SetSessionBadge(false);
            return;
        }

        var title = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Username : p.DisplayName;
        SelectedTitleText.Text = string.IsNullOrWhiteSpace(title) ? "Selected profile" : title;
        SelectedSubtitleText.Text = $"@{p.Username}" + (p.UserId > 0 ? $" • ID {p.UserId}" : string.Empty) + $" • Group: {p.Group}";
        SetSessionBadge(AccountAuthStore.HasLogin(p.Id));
    }

    private void SetSessionBadge(bool hasSession)
    {
        SessionStateText.Text = hasSession ? "Encrypted account login saved" : "Add login via browser";
        SessionStateBadge.Background = hasSession ? new SolidColorBrush(Color.FromRgb(22, 59, 54)) : new SolidColorBrush(Color.FromRgb(24, 50, 87));
        SessionStateBadge.BorderBrush = hasSession ? new SolidColorBrush(Color.FromRgb(84, 209, 176)) : new SolidColorBrush(Color.FromRgb(74, 122, 188));
    }

    private void Sort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (AccountsList is not null) Refresh();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveProfile(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveProfile(1);

    private void MoveProfile(int offset)
    {
        if (_selectedId is null) return;
        // Preserve the current displayed order when switching to manual mode.
        var ordering = _displayed.ToList();
        int index = ordering.FindIndex(p => p.Id == _selectedId);
        int target = index + offset;
        if (index < 0 || target < 0 || target >= ordering.Count) return;
        (ordering[index], ordering[target]) = (ordering[target], ordering[index]);
        for (int i = 0; i < ordering.Count; i++) ordering[i].SortOrder = i + 1;
        ProfileStore.Save(_profiles);
        SortBox.SelectedIndex = 3;
        Refresh();
    }

    private async Task LoadAvatarsAsync()
    {
        try
        {
            var candidates = _profiles.Where(p => p.UserId > 0 && string.IsNullOrWhiteSpace(p.AvatarUrl)).Take(16).ToList();
            if (candidates.Count == 0) return;
            // Small bounded batches to reduce request pressure on public endpoints.
            foreach (var batch in candidates.Chunk(4))
            {
                var imageUrls = await Task.WhenAll(batch.Select(async p => (p, url: await RobloxApi.GetAvatarAsync(p.UserId))));
                foreach (var item in imageUrls)
                    if (!string.IsNullOrWhiteSpace(item.url)) item.p.AvatarUrl = item.url;
            }
            if (candidates.Any(p => !string.IsNullOrWhiteSpace(p.AvatarUrl)))
            {
                ProfileStore.Save(_profiles);
                Refresh();
            }
        }
        catch { /* Public thumbnails are optional; missing network must not break the app. */ }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (AccountsList is not null)
            Refresh();
    }

    private void Account_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (AccountsList.SelectedItem is not AccountProfile p)
        {
            _selectedId = null;
            UpdateSelectionSummary(null);
            return;
        }

        _selectedId = p.Id;
        UsernameBox.Text = p.Username;
        DisplayBox.Text = p.DisplayName;
        IdBox.Text = p.UserId > 0 ? p.UserId.ToString() : string.Empty;
        GroupBox.Text = string.IsNullOrWhiteSpace(p.Group) ? "General" : p.Group;
        NoteBox.Text = p.Note;
        FavoriteBox.IsChecked = p.Favorite;
        PreferredPlaceBox.Text = p.PreferredPlaceId > 0 ? p.PreferredPlaceId.ToString() : string.Empty;
        PreferredJobBox.Text = p.PreferredJobId;
        UpdateSelectionSummary(p);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string username = UsernameBox.Text.Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("Enter an account username first.", "Missing username", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(IdBox.Text) && (!long.TryParse(IdBox.Text, out long parsed) || parsed <= 0))
        {
            MessageBox.Show("User ID must be a positive number.", "Invalid user ID", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        long userId = string.IsNullOrWhiteSpace(IdBox.Text) ? 0 : long.Parse(IdBox.Text);
        var profile = _profiles.FirstOrDefault(x => x.Id == _selectedId) ?? new AccountProfile();

        if (_profiles.Any(x => x.Id != profile.Id &&
                               (x.Username.Equals(username, StringComparison.OrdinalIgnoreCase) ||
                                (userId > 0 && x.UserId == userId))))
        {
            MessageBox.Show("This account is already in the list.", "Duplicate profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        profile.Username = username;
        profile.DisplayName = DisplayBox.Text.Trim();
        profile.UserId = userId;
        profile.Group = string.IsNullOrWhiteSpace(GroupBox.Text) ? "General" : GroupBox.Text.Trim();
        profile.Note = NoteBox.Text.Trim();
        profile.Favorite = FavoriteBox.IsChecked == true;
        if (!string.IsNullOrWhiteSpace(PreferredPlaceBox.Text) &&
            (!long.TryParse(PreferredPlaceBox.Text, out long preferred) || preferred <= 0))
        {
            MessageBox.Show("Preferred Place ID must be a positive number.");
            return;
        }
        profile.PreferredPlaceId = long.TryParse(PreferredPlaceBox.Text, out long gameId) ? gameId : 0;
        profile.PreferredJobId = PreferredJobBox.Text.Trim();

        if (!_profiles.Any(x => x.Id == profile.Id))
            _profiles.Add(profile);

        try
        {
            ProfileStore.Save(_profiles);
            _selectedId = profile.Id;
            Refresh();
            AccountsList.SelectedItem = _displayed.FirstOrDefault(x => x.Id == profile.Id);
            StatusText.Text = $"Saved profile @{profile.Username}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _selectedId = null;
        AccountsList.SelectedItem = null;
        ClearEditor();
        UpdateSelectionSummary(null);
        StatusText.Text = "Creating a new profile. Enter details, then click Save profile.";
    }

    private void ClearEditor()
    {
        UsernameBox.Clear();
        DisplayBox.Clear();
        IdBox.Clear();
        GroupBox.Text = "General";
        NoteBox.Clear();
        FavoriteBox.IsChecked = false;
        PreferredPlaceBox.Clear();
        PreferredJobBox.Clear();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var p = _profiles.FirstOrDefault(x => x.Id == _selectedId);
        if (p is null)
            return;

        if (MessageBox.Show($"Remove @{p.Username}?", "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _profiles.Remove(p);
        try
        {
            ProfileStore.Save(_profiles);
            AccountAuthStore.Delete(p.Id);
            SessionSwitcher.Delete(p.Id);
            ClearEditor();
            _selectedId = null;
            Refresh();
            StatusText.Text = $"Deleted profile @{p.Username}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Delete failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void Safe(Action a)
    {
        try
        {
            a();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Action failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void VerifyClient_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionBusy) return;
        _sessionBusy = true;
        try
        {
            StatusText.Text = "Verifying installed Roblox client account with Roblox…";
            var actual = await RobloxSessionIdentity.CheckCurrentAsync();
            if (actual is null)
            {
                StatusText.Text = "No valid desktop-client session was found. Sign in through Roblox desktop, close it, and retry.";
                MessageBox.Show("No valid desktop Roblox login was found. Browser sign-in alone does NOT create a client session. Open Roblox desktop, log in, close all Roblox windows, then retry.",
                    "Desktop client login not verified", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            StatusText.Text = $"Roblox verified @{actual.Name} (ID {actual.Id}).";
            MessageBox.Show($"Desktop client authenticated as @{actual.Name}\nUser ID: {actual.Id}",
                "Verified Roblox identity", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Identity verification", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _sessionBusy = false; }
    }

    private async void SaveSession_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionBusy) return;
        _sessionBusy = true;
        try
        {
            var login = new BrowserLoginWindow { Owner = this };
            if (login.ShowDialog() != true || login.CapturedUser is null || login.CapturedCookie is null) return;
            var actual = login.CapturedUser;
            // Every captured identity maps to ONE unique profile. Never bind the wrong login to a selected profile.
            var profile = _profiles.FirstOrDefault(p => p.UserId > 0 && p.UserId == actual.Id)
                ?? _profiles.FirstOrDefault(p => p.Username.Equals(actual.Name, StringComparison.OrdinalIgnoreCase));
            bool created = profile == null;
            profile ??= new AccountProfile { Username = actual.Name, UserId = actual.Id, Group = "General" };
            if (profile.UserId > 0 && profile.UserId != actual.Id)
                throw new InvalidOperationException("Existing profile has the same username but a different Roblox user ID. No login saved.");
            profile.Username = actual.Name;
            profile.UserId = actual.Id;
            if (string.IsNullOrWhiteSpace(profile.DisplayName)) profile.DisplayName = actual.DisplayName;
            // Persist metadata first. A failed profile save must not create orphaned sessions.
            if (created) _profiles.Add(profile);
            try { ProfileStore.Save(_profiles); }
            catch { if (created) _profiles.Remove(profile); throw; }
            AccountAuthStore.Save(profile.Id, login.CapturedCookie);
            _selectedId = profile.Id;
            Refresh();
            AccountsList.SelectedItem = _displayed.FirstOrDefault(p => p.Id == profile.Id);
            StatusText.Text = $"Encrypted browser login saved for @{actual.Name} (ID {actual.Id}).";
            MessageBox.Show(this,
                $"Account @{actual.Name} added. You can now add your next Roblox account using a NEW login browser. Do not log out of this one.",
                "Account saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { StatusText.Text = ex.Message; MessageBox.Show(this, ex.Message, "Account login", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _sessionBusy = false; }
    }

    private async void RestoreSession_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionBusy) return;
        var profile = _profiles.FirstOrDefault(p => p.Id == _selectedId);
        if (profile is null) { MessageBox.Show("Select an account first."); return; }
        _sessionBusy = true;
        try
        {
            StatusText.Text = "Checking the selected account's saved login…";
            var actual = await RobloxTicketLauncher.VerifySavedAsync(profile);
            StatusText.Text = $"Roblox verified saved account @{actual.Name} (ID {actual.Id}).";
            MessageBox.Show(this, StatusText.Text, "Saved login verified", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { StatusText.Text = ex.Message; MessageBox.Show(this, ex.Message, "Login check", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _sessionBusy = false; }
    }

    private async Task LaunchSelectedGameAsync(bool preferredOnly)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == _selectedId)
            ?? throw new InvalidOperationException("Select an account first.");
        long place = profile.PreferredPlaceId;
        if (!preferredOnly && long.TryParse(PlaceBox.Text, out long entered) && entered > 0) place = entered;
        if (place <= 0) throw new InvalidOperationException("Enter a game Place ID (or save your preferred Place ID) first.");
        string? jobId = !string.IsNullOrWhiteSpace(profile.PreferredJobId) && place == profile.PreferredPlaceId
            ? profile.PreferredJobId : null;
        StatusText.Text = $"Requesting a fresh Roblox authentication ticket for @{profile.Username}…";
        StatusText.Text = await RobloxTicketLauncher.LaunchAsync(profile, place, jobId);
        profile.LastSelectedUtc = DateTimeOffset.UtcNow;
        ProfileStore.Save(_profiles);
        GameLibraryStore.AddRecent(GameLibraryStore.Load(), place);
    }

    private async void RestoreAndLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionBusy) return;
        _sessionBusy = true;
        try { await LaunchSelectedGameAsync(preferredOnly: false); }
        catch (Exception ex) { StatusText.Text = ex.Message; MessageBox.Show(this, ex.Message, "Selected account launch", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _sessionBusy = false; }
    }

    private void Login_Click(object sender, RoutedEventArgs e) => Safe(() => { StatusText.Text = RobloxService.LaunchDesktop(); });
    private void IsolatedBrowser_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var p = _profiles.FirstOrDefault(x => x.Id == _selectedId) ?? throw new InvalidOperationException("Select an account first.");
        RobloxService.OpenIsolatedAccountBrowser(p.Id);
        StatusText.Text = $"Launched a separate Edge profile for @{p.Username}. Sign in in that browser once; it is not the Roblox desktop login.";
    });
    private void BrowserLogin_Click(object sender, RoutedEventArgs e) => Safe(RobloxService.BrowserLogin);
    private async void Lookup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var username = UsernameBox.Text.Trim();
            StatusText.Text = "Looking up public Roblox profile…";
            var account = await RobloxApi.LookupUsernameAsync(username);
            if (account is null) { MessageBox.Show("No exact Roblox username match found."); return; }
            IdBox.Text = account.Id.ToString();
            DisplayBox.Text = account.DisplayName;
            StatusText.Text = $"Found verified public user @{account.Name} (ID {account.Id}). Click Save profile to keep it.";
            var p = _profiles.FirstOrDefault(x => x.Id == _selectedId);
            if (p is not null && p.Username.Equals(account.Name, StringComparison.OrdinalIgnoreCase))
            {
                p.AvatarUrl = account.AvatarUrl;
                ProfileStore.Save(_profiles);
                Refresh();
            }
        }
        catch (Exception ex) { StatusText.Text = ex.Message; MessageBox.Show(ex.Message, "Roblox lookup", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void AdvancedSettings_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var dialog = new AdvancedWindow(_advancedSettings, _localApi, _watcher,
            () => _profiles.FirstOrDefault(x => x.Id == _selectedId), _profiles, Refresh) { Owner = this };
        dialog.ShowDialog();
        StatusText.Text = "Advanced controls closed. Watcher: " + (_advancedSettings.WatcherEnabled ? "on" : "off") +
                          "; local API: " + (_localApi.IsRunning ? "on" : "off") + ".";
    });

    private void OpenTools_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var p = _profiles.FirstOrDefault(x => x.Id == _selectedId);
        var window = new ToolsWindow(_profiles, Refresh, p?.PreferredPlaceId ?? 0) { Owner = this };
        window.ShowDialog();
        Refresh();
    });

    private async void OpenPreferred_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionBusy) return;
        _sessionBusy = true;
        try { await LaunchSelectedGameAsync(preferredOnly: true); }
        catch (Exception ex) { StatusText.Text = ex.Message; MessageBox.Show(this, ex.Message, "Preferred game launch", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _sessionBusy = false; }
    }

    private void Home_Click(object sender, RoutedEventArgs e) => Safe(RobloxService.Home);

    private void Profile_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var p = _profiles.FirstOrDefault(x => x.Id == _selectedId) ??
                throw new InvalidOperationException("Select an account first.");
        RobloxService.OpenProfile(p.UserId);
    });

    private void Game_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        if (!long.TryParse(PlaceBox.Text, out long place) || place <= 0)
            throw new InvalidOperationException("Enter a valid place ID first.");
        RobloxService.OpenGame(place);
        var data = GameLibraryStore.Load();
        GameLibraryStore.AddRecent(data, place);
        StatusText.Text = $"Opened game {place} and saved it to recent games.";
    });

    private void Folder_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        Directory.CreateDirectory(ProfileStore.Folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ProfileStore.Folder) { UseShellExecute = true });
    });

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateWindowVisualState();
    }

    private void Window_StateChanged(object? sender, EventArgs e) => UpdateWindowVisualState();

    private void UpdateWindowVisualState()
    {
        if (WindowState == WindowState.Maximized)
        {
            RootBorder.Margin = new Thickness(0);
            RootBorder.CornerRadius = new CornerRadius(0);
            MaximizeGlyph.Text = "❐";
        }
        else
        {
            RootBorder.Margin = new Thickness(8);
            RootBorder.CornerRadius = new CornerRadius(22);
            MaximizeGlyph.Text = "☐";
        }
    }
}
