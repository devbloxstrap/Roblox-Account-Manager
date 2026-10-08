using System.Diagnostics;
using System.Windows.Threading;

namespace RAM.Modern.Services;

/// <summary>Observes local Roblox processes. Auto relaunch is opt-in, limited, and not an AFK detector.</summary>
public sealed class RobloxWatcher : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(7) };
    private readonly Func<AdvancedSettings> _settings;
    private bool _wasRunning;
    private bool _initialized;
    private DateTime _lastRelaunch = DateTime.MinValue;
    private readonly Queue<DateTime> _launches = new();

    public event Action<string>? StatusChanged;
    public bool IsRunning { get; private set; }
    public int ProcessCount { get; private set; }

    public RobloxWatcher(Func<AdvancedSettings> settings)
    {
        _settings = settings;
        _timer.Tick += OnTick;
        _timer.Start();
        OnTick(this, EventArgs.Empty);
    }

    public void PollNow() => OnTick(this, EventArgs.Empty);

    private static int CountProcesses()
    {
        int total = 0;
        foreach (var name in new[] { "RobloxPlayerBeta", "RobloxPlayer" })
        {
            var matches = Process.GetProcessesByName(name);
            total += matches.Length;
            foreach (var proc in matches) proc.Dispose();
        }
        return total;
    }

    private void OnTick(object? sender, EventArgs args)
    {
        try
        {
            var settings = _settings();
            if (!settings.WatcherEnabled) { _initialized = false; IsRunning = false; ProcessCount = 0; return; }
            ProcessCount = CountProcesses();
            IsRunning = ProcessCount > 0;
            if (!_initialized)
            {
                _initialized = true;
                _wasRunning = IsRunning;
                StatusChanged?.Invoke(IsRunning ? "Roblox Player detected." : "Watcher active. Roblox Player is not running.");
                return;
            }
            if (_wasRunning == IsRunning) return;
            bool justExited = _wasRunning && !IsRunning;
            _wasRunning = IsRunning;
            StatusChanged?.Invoke(IsRunning ? "Roblox Player process started." : "Roblox Player process exited.");
            if (!justExited || !settings.AutoRelaunch) return;
            if (settings.RequireDisconnectSignal && !RobloxLogSignals.SawRecentDisconnect(out _))
            {
                StatusChanged?.Invoke("Roblox closed without a recent disconnect signal. Auto-relaunch skipped (manual close or unrecognized failure).");
                return;
            }
            var now = DateTime.UtcNow;
            while (_launches.Count > 0 && now - _launches.Peek() > TimeSpan.FromHours(1)) _launches.Dequeue();
            if (now - _lastRelaunch < TimeSpan.FromMinutes(5) || _launches.Count >= 3)
            {
                StatusChanged?.Invoke("Watcher: relaunch limit reached; no action taken.");
                return;
            }
            _lastRelaunch = now;
            _launches.Enqueue(now);
            // Only relaunches when a previously observed Roblox Player process closes.
            // Cannot distinguish manual close from disconnect/AFK termination.
            if (settings.RelaunchPlaceId > 0) RobloxService.OpenGame(settings.RelaunchPlaceId);
            else RobloxService.LaunchDesktop();
            StatusChanged?.Invoke("Watcher requested Roblox relaunch. Check Roblox; account login is not guaranteed.");
        }
        catch (Exception ex) { StatusChanged?.Invoke("Watcher error: " + ex.Message); }
    }

    public void Dispose() => _timer.Stop();
}
