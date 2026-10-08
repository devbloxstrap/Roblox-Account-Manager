using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RAM.Modern.Models;
using RAM.Modern.Services;

namespace RAM.Modern;

/// <summary>The user logs in at Roblox.com in a separate browser; a deliberate click
/// captures only that session, validates account identity with Roblox, and saves it encrypted.</summary>
public sealed class BrowserLoginWindow : Window
{
    private readonly BrowserLoginCapture _capture = new();
    private readonly Button _captureButton;
    private readonly TextBlock _status;
    public string? CapturedCookie { get; private set; }
    public RobloxUser? CapturedUser { get; private set; }

    public BrowserLoginWindow()
    {
        Title = "Add Roblox Account — Official Login";
        Width = 610;
        Height = 370;
        MinWidth = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(13, 24, 43));
        Foreground = Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(28) };
        Content = panel;
        panel.Children.Add(new TextBlock
        {
            Text = "Add a Roblox account securely", FontSize = 24, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 16)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "1. An isolated Edge/Chrome window opens the official roblox.com login.\n" +
                   "2. Sign in there and complete any Roblox verification. Do not log out.\n" +
                   "3. Click Capture below. RAM will read this browser's Roblox session (not your password), verify the account and encrypt it for your Windows user.\n" +
                   "4. Repeat for another account in a NEW browser window. Existing accounts stay signed in.",
            TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(0, 0, 0, 18)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Security: Account sessions are equivalent to login credentials. Never share session files or screenshots of tokens.",
            Foreground = new SolidColorBrush(Color.FromRgb(250, 190, 110)),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16)
        });
        _status = new TextBlock { Text = "Opening official login window…", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(_status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        _captureButton = new Button { Content = "Capture my signed-in account", Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 10, 0) };
        _captureButton.Click += Capture_Click;
        actions.Children.Add(_captureButton);
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 10, 14, 10) };
        cancel.Click += (_, _) => { DialogResult = false; };
        actions.Children.Add(cancel);
        panel.Children.Add(actions);
        Loaded += (_, _) =>
        {
            try { _capture.Start(); _status.Text = "Official Roblox login opened in " + _capture.EngineName + ". Complete login, then Capture."; }
            catch (Exception ex) { _status.Text = ex.Message; _captureButton.IsEnabled = false; }
        };
        Closed += (_, _) => _capture.Dispose();
    }

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        _captureButton.IsEnabled = false;
        try
        {
            _status.Text = "Checking isolated browser login with Roblox…";
            string cookie = await _capture.CaptureCookieAsync();
            var account = await RobloxTicketLauncher.VerifyAsync(cookie);
            if (account == null) throw new InvalidOperationException("Roblox rejected that browser login. Complete sign-in and try again.");
            CapturedCookie = cookie;
            CapturedUser = account;
            _status.Text = "Verified @" + account.Name;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            // Never display raw cookies or server headers.
            _status.Text = "Login not captured: " + ex.Message;
        }
        finally { _captureButton.IsEnabled = true; }
    }
}
