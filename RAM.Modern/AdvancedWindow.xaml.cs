using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using RAM.Modern.Models;
using Microsoft.Win32;
using RAM.Modern.Services;

namespace RAM.Modern;

public partial class AdvancedWindow : Window
{
    private readonly AdvancedSettings _settings;
    private readonly LocalDeveloperApi _api;
    private readonly RobloxWatcher _watcher;
    private readonly Func<AccountProfile?> _selectedProfile;
    private readonly List<AccountProfile> _profiles;
    private readonly Action _refreshProfiles;

    public AdvancedWindow(AdvancedSettings settings, LocalDeveloperApi api, RobloxWatcher watcher, Func<AccountProfile?> selectedProfile, List<AccountProfile> profiles, Action refreshProfiles)
    {
        InitializeComponent();
        _settings = settings;
        _api = api;
        _watcher = watcher;
        _selectedProfile = selectedProfile;
        _profiles = profiles;
        _refreshProfiles = refreshProfiles;
        WatcherEnabledCheck.IsChecked = settings.WatcherEnabled;
        RelaunchCheck.IsChecked = settings.AutoRelaunch;
        NotificationsCheck.IsChecked = settings.WatcherNotifications;
        RequireDisconnectCheck.IsChecked = settings.RequireDisconnectSignal;
        RelaunchPlaceBox.Text = settings.RelaunchPlaceId.ToString();
        ApiEnabledCheck.IsChecked = settings.LocalApiEnabled;
        ApiPortBox.Text = settings.LocalApiPort.ToString();
        DeveloperModeCheck.IsChecked = settings.DeveloperMode;
        ApiStatusText.Text = api.IsRunning ? $"API running on 127.0.0.1:{api.Port}" : "API is stopped.";
        ThemePresetBox.SelectedIndex = Math.Max(0, Array.IndexOf(ThemeManager.Presets, settings.ThemePreset));
        CustomAccentBox.Text = settings.CustomAccent;
        RefreshGroups();
        ThemeManager.Apply(this, _settings);
        CheckProcesses_Click(this, new RoutedEventArgs());
    }

    private void Run(Action action, string title)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (!int.TryParse(ApiPortBox.Text.Trim(), out int port) || port is < 1024 or > 65535)
            throw new ArgumentException("Enter a valid local API port between 1024 and 65535.");
        if (!long.TryParse(RelaunchPlaceBox.Text.Trim(), out long placeId) || placeId < 0)
            throw new ArgumentException("Relaunch Place ID must be 0 or a positive number.");
        // Persist only after validation. Local API starts only with explicit opt-in.
        bool enable = ApiEnabledCheck.IsChecked == true;
        if (enable)
        {
            // If port is blocked, surface the error before persisting the new setting.
            _api.Start(port);
        }
        else _api.Stop();
        _settings.WatcherEnabled = WatcherEnabledCheck.IsChecked == true;
        _settings.AutoRelaunch = RelaunchCheck.IsChecked == true;
        _settings.WatcherNotifications = NotificationsCheck.IsChecked == true;
        _settings.RequireDisconnectSignal = RequireDisconnectCheck.IsChecked == true;
        _settings.RelaunchPlaceId = placeId;
        _settings.LocalApiEnabled = enable;
        _settings.LocalApiPort = port;
        _settings.DeveloperMode = DeveloperModeCheck.IsChecked == true;
        _settings.ThemePreset = ((ComboBoxItem)ThemePresetBox.SelectedItem).Content.ToString() ?? "Aurora";
        _settings.CustomAccent = CustomAccentBox.Text.Trim();
        _ = ThemeManager.ParseColor(_settings.CustomAccent);
        foreach (Window window in Application.Current.Windows) ThemeManager.Apply(window, _settings);
        SettingsStore.Save(_settings);
        _watcher.PollNow();
        ApiStatusText.Text = _api.IsRunning ? $"API running on 127.0.0.1:{_api.Port}" : "API stopped.";
        MessageBox.Show(this, "Advanced settings saved.", "RAM settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }, "Settings error");

    private void PreviewTheme_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        string preset = ((ComboBoxItem)ThemePresetBox.SelectedItem).Content.ToString() ?? "Aurora";
        var sample = new AdvancedSettings { ThemePreset = preset, CustomAccent = CustomAccentBox.Text.Trim() };
        _ = ThemeManager.ParseColor(sample.CustomAccent);
        foreach (Window window in Application.Current.Windows) ThemeManager.Apply(window, sample);
        ThemeStatusText.Text = $"Preview applied: {preset}. Click Save settings to keep it after restart.";
    }, "Theme preview");

    private void RefreshGroups()
    {
        GroupPrioritiesList.ItemsSource = _profiles.Select(p => GroupSort.Parse(p.Group))
            .Distinct()
            .OrderBy(p => p.Priority).ThenBy(p => p.DisplayName)
            .Select(p => $"{p.Priority:000}  •  {p.DisplayName}").ToList();
    }

    private void GroupPriorities_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupPrioritiesList.SelectedItem is not string text || text.Length < 7) return;
        PriorityNumberBox.Text = text[..3];
        PriorityGroupBox.Text = text[8..];
    }

    private void ApplyGroupPriority_Click(object sender, RoutedEventArgs e) => UpdateGroupPriority(true);
    private void RemoveGroupPriority_Click(object sender, RoutedEventArgs e) => UpdateGroupPriority(false);

    private void UpdateGroupPriority(bool apply)
    {
        Run(() =>
        {
            string name = PriorityGroupBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter an existing group name.");
            int n = 500;
            if (apply && (!int.TryParse(PriorityNumberBox.Text.Trim(), out n) || n is < 0 or > 999))
                throw new ArgumentException("Priority must be 0–999.");
            int changed = 0;
            foreach (var p in _profiles)
                if (GroupSort.Parse(p.Group).DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    p.Group = apply ? $"{n:000} {name}" : name;
                    changed++;
                }
            if (changed == 0) throw new InvalidOperationException("No profiles with that group name were found.");
            ProfileStore.Save(_profiles);
            _refreshProfiles();
            RefreshGroups();
            GroupSortStatusText.Text = $"Updated group priority for {changed} profile(s).";
        }, "Group priority");
    }

    private void CheckProcesses_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        int count = RobloxService.RunningPlayerCount();
        WatcherCountText.Text = $"Roblox Player processes found: {count}. Watcher active: {_settings.WatcherEnabled}.";
    }, "Watcher status");

    private void CheckLogSignal_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        bool found = RobloxLogSignals.SawRecentDisconnect(out string detail);
        WatcherCountText.Text = (found ? "Detected: " : "Not detected: ") + detail;
    }, "Roblox client logs");

    private void ReadFps_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var fps = FpsSettings.Read();
        FpsStatusText.Text = fps is null ? "No supported FramerateCap setting found. Launch Roblox and check in-game Settings." : $"Current stored FPS cap: {fps.Value}.";
    }, "Read FPS");

    private void ApplyFps_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        int fps = int.Parse(((ComboBoxItem)FpsPresetBox.SelectedItem).Content.ToString()!);
        FpsSettings.Apply(fps);
        FpsStatusText.Text = $"Saved {fps} FPS to Roblox settings XML. Roblox may apply or overwrite it during launch.";
    }, "FPS settings");

    private void RestoreFps_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        FpsSettings.RestoreBackup();
        FpsStatusText.Text = "Restored original Roblox XML backup.";
    }, "Restore FPS");

    private void CopyToken_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (MessageBox.Show(this, "Copy the private localhost API key to your clipboard? Other programs may read clipboard contents. Never share the token.", "Sensitive token", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Clipboard.SetText(LocalDeveloperApi.GetTokenHex());
        ApiStatusText.Text = "Token copied. Keep it private and clear the clipboard when finished.";
    }, "API token");

    private void RotateToken_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (MessageBox.Show(this, "Rotate the API key? All existing local scripts using the previous token will stop authenticating.", "Rotate API key", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        LocalDeveloperApi.ResetToken();
        ApiStatusText.Text = "API key rotated. Existing scripts must use the new key.";
    }, "API token");

    private void ApiExample_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        Clipboard.SetText($"$token = '<paste-token-here>'\nInvoke-RestMethod -Uri 'http://127.0.0.1:{_settings.LocalApiPort}/v1/status' -Headers @{{ Authorization = \"Bearer $token\" }}");
        ApiStatusText.Text = "PowerShell example copied (token placeholder only).";
    }, "API example");

    private void ExportEncrypted_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var password = BackupPasswordBox.Password;
        if (password.Length < 12) throw new InvalidOperationException("Use at least 12 characters for your backup password.");
        var dialog = new SaveFileDialog { Filter = "RAM encrypted metadata (*.rammeta)|*.rammeta", FileName = "RAM-profiles.rammeta" };
        if (dialog.ShowDialog(this) != true) return;
        EncryptedProfileBackup.Export(dialog.FileName, password, _profiles);
        BackupPasswordBox.Clear();
        BackupStatusText.Text = $"Encrypted {_profiles.Count} profile metadata entries. No session files included.";
    }, "Export encrypted backup");

    private void ImportEncrypted_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        string password = BackupPasswordBox.Password;
        var dialog = new OpenFileDialog { Filter = "RAM encrypted metadata (*.rammeta)|*.rammeta" };
        if (dialog.ShowDialog(this) != true) return;
        var incoming = EncryptedProfileBackup.Import(dialog.FileName, password);
        BackupPasswordBox.Clear();
        if (MessageBox.Show(this, $"Import {incoming.Count} profile entries? Matching usernames or Roblox user IDs will be skipped. Existing data is preserved.",
                "Confirm import", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        int added = 0;
        foreach (var profile in incoming)
        {
            if (_profiles.Any(p => p.Username.Equals(profile.Username, StringComparison.OrdinalIgnoreCase) ||
                (p.UserId > 0 && profile.UserId > 0 && p.UserId == profile.UserId))) continue;
            _profiles.Add(profile);
            added++;
        }
        ProfileStore.Save(_profiles);
        _refreshProfiles();
        BackupStatusText.Text = $"Imported {added} new profiles. Session backups are not transferred.";
    }, "Import encrypted backup");

    private void OpenRoblox_Click(object sender, RoutedEventArgs e) => Run(() => ControlsStatusText.Text = RobloxService.LaunchDesktop(), "Roblox launch");

    private async void OpenPreferred_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var p = _selectedProfile() ?? throw new InvalidOperationException("Select an account in the main window first.");
            ControlsStatusText.Text = await RobloxTicketLauncher.LaunchAsync(p, p.PreferredPlaceId, p.PreferredJobId);
        }
        catch (Exception ex) { ControlsStatusText.Text = ex.Message; MessageBox.Show(this, ex.Message, "Preferred game", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void CloseRoblox_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (MessageBox.Show(this, "Close all Roblox Player windows now? Unsaved gameplay may be lost.", "Close Roblox Player", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        int count = 0;
        foreach (var name in new[] { "RobloxPlayerBeta", "RobloxPlayer" })
        foreach (var p in Process.GetProcessesByName(name))
        {
            using (p)
            {
                if (p.HasExited) continue;
                if (!p.CloseMainWindow()) p.Kill(entireProcessTree: false);
                count++;
            }
        }
        ControlsStatusText.Text = $"Requested close for {count} Roblox Player processes. Auto-relaunch may restart them if enabled.";
    }, "Roblox close");

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var picker = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = "RAM-diagnostics.txt" };
        if (picker.ShowDialog(this) != true) return;
        File.WriteAllText(picker.FileName, DiagnosticsReport.Generate(_profiles, _settings, _api));
        ControlsStatusText.Text = "Diagnostics exported without passwords, tokens, usernames or cookies.";
    }, "Export diagnostics");

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
