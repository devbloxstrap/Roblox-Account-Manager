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

        foreach (var p in query
                     .OrderByDescending(p => p.Favorite)
                     .ThenBy(p => p.Group)
                     .ThenBy(p => string.IsNullOrWhiteSpace(p.DisplayName) ? p.Username : p.DisplayName))
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
            ? "Ready. Create your first profile to begin. Session snapshots are encrypted for the current Windows user."
            : $"Ready. {_profiles.Count} profile{(_profiles.Count == 1 ? string.Empty : "s")} available. Saved sessions are encrypted for the current Windows user.";
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
        SetSessionBadge(SessionSwitcher.HasSnapshot(p.Id));
    }

    private void SetSessionBadge(bool hasSession)
    {
        SessionStateText.Text = hasSession ? "Saved session available" : "No saved session";
        SessionStateBadge.Background = hasSession ? new SolidColorBrush(Color.FromRgb(22, 59, 54)) : new SolidColorBrush(Color.FromRgb(24, 50, 87));
        SessionStateBadge.BorderBrush = hasSession ? new SolidColorBrush(Color.FromRgb(84, 209, 176)) : new SolidColorBrush(Color.FromRgb(74, 122, 188));
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

    private void SaveSession_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var p = _profiles.FirstOrDefault(x => x.Id == _selectedId) ??
                throw new InvalidOperationException("Select a profile first.");

        if (MessageBox.Show($"Save the current Roblox client login for @{p.Username}?\n\nMake sure the client is signed in with the same account. This action does not verify the username inside the login file.",
                "Confirm session save", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        SessionSwitcher.SaveCurrent(p.Id);
        SetSessionBadge(true);
        StatusText.Text = $"Saved an encrypted session snapshot for @{p.Username}.";
    });

    private void RestoreSession_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var p = _profiles.FirstOrDefault(x => x.Id == _selectedId) ??
                throw new InvalidOperationException("Select a profile first.");

        if (MessageBox.Show($"Restore the saved Roblox client session for @{p.Username}?\n\nAll Roblox windows must be closed. This replaces the local client login file and Roblox may still ask you to sign in again if the session is expired or revoked.",
                "Confirm session restore", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        SessionSwitcher.Restore(p.Id);
        p.LastSelectedUtc = DateTimeOffset.UtcNow;
        ProfileStore.Save(_profiles);
        StatusText.Text = $"Restored the local client session for @{p.Username}. Open Roblox to verify it.";
        UpdateSelectionSummary(p);
    });

    private void Login_Click(object sender, RoutedEventArgs e) => Safe(RobloxService.Login);
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
