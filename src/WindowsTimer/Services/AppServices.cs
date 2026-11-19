using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using WindowsTimer.Models;
using WindowsTimer.ViewModels;

namespace WindowsTimer.Services;

public sealed class AlarmPlayer : IDisposable
{
    private readonly object _gate = new();
    private System.Media.SoundPlayer? _player;
    private MemoryStream? _stream;
    private CancellationTokenSource? _loop;
    private AlertSoundId _cachedSound;
    private double _cachedVolume = -1;
    private byte[]? _cachedWav;

    public void Preview(AppSettings settings) => Play(settings);

    public void Start(AppSettings settings)
    {
        Stop();
        Play(settings);
        if (!settings.RepeatUntilDismissed || settings.Mute || settings.Volume <= 0.001)
            return;

        var cts = new CancellationTokenSource();
        lock (_gate)
            _loop = cts;
        _ = RepeatAsync(settings, cts.Token);
    }

    public void Stop()
    {
        CancellationTokenSource? loop;
        lock (_gate)
        {
            loop = _loop;
            _loop = null;
            try
            {
                _player?.Stop();
            }
            catch (Exception ex)
            {
                AppLog.Warn("Could not stop notification sound. " + ex.GetType().Name);
            }
        }

        if (loop == null)
            return;

        try
        {
            loop.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        loop.Dispose();
    }

    public void Dispose() => Stop();

    private async Task RepeatAsync(AppSettings settings, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(2600), token);
                Play(settings);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Play(AppSettings settings)
    {
        if (settings.Mute || settings.Volume <= 0.001)
            return;

        try
        {
            var wav = GetWav(settings);
            var stream = new MemoryStream(wav, writable: false);
            var player = new System.Media.SoundPlayer(stream);
            player.Load();
            lock (_gate)
            {
                try
                {
                    _player?.Stop();
                }
                catch
                {
                    // Replacing the player is still safe.
                }

                _player?.Dispose();
                _stream?.Dispose();
                _player = player;
                _stream = stream;
            }

            player.Play();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Notification sound could not be played. " + ex.GetType().Name);
        }
    }

    private byte[] GetWav(AppSettings settings)
    {
        if (_cachedWav != null
            && _cachedSound == settings.AlertSound
            && Math.Abs(_cachedVolume - settings.Volume) < 0.001)
        {
            return _cachedWav;
        }

        _cachedSound = settings.AlertSound;
        _cachedVolume = settings.Volume;
        _cachedWav = ToneLibrary.CreateWav(settings.AlertSound, settings.Volume);
        return _cachedWav;
    }
}

public sealed class NotificationService
{
    public void ShowTimeUp()
    {
        try
        {
            var xml = new XmlDocument();
            xml.LoadXml("""
                <toast duration="short">
                  <visual>
                    <binding template="ToastGeneric">
                      <text>Time's Up</text>
                      <text>Your countdown has finished.</text>
                    </binding>
                  </visual>
                </toast>
                """);
            var toast = new ToastNotification(xml);
            ToastNotificationManager.CreateToastNotifier(NativeMethods.AppUserModelId).Show(toast);
        }
        catch (Exception ex)
        {
            AppLog.Info("Windows notification is unavailable. The in-app alert is still shown. " + ex.GetType().Name);
        }
    }
}

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowsTimer";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Windows startup key is unavailable.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("The application path is unavailable.");

        key.SetValue(ValueName, $"\"{path}\"");
    }
}

public sealed class ThemeService : IDisposable
{
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private ThemePreference _preference = ThemePreference.System;
    private bool _disposed;

    public ThemeService(System.Windows.Threading.Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Apply(ThemePreference preference)
    {
        _preference = preference;
        var dark = preference switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            _ => IsSystemInDarkMode()
        };

        var uri = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
        var dictionary = new System.Windows.ResourceDictionary { Source = uri };
        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (merged.Count == 0)
            merged.Add(dictionary);
        else
            merged[0] = dictionary;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    public static bool IsSystemInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not read the Windows theme. " + ex.GetType().Name);
            return false;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_disposed || e.Category != UserPreferenceCategory.General || _preference != ThemePreference.System)
            return;

        _dispatcher.BeginInvoke(() =>
        {
            if (!_disposed && _preference == ThemePreference.System)
                Apply(ThemePreference.System);
        });
    }
}

public static class SingleInstance
{
    private const string MutexName = @"Local\WindowsTimer.SingleInstance";
    private const string EventName = @"Local\WindowsTimer.Activate";
    private static Mutex? _mutex;
    private static EventWaitHandle? _event;
    private static Thread? _thread;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var created);
        if (!created)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        _event = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        return true;
    }

    public static void SignalExisting()
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(EventName);
            handle.Set();
        }
        catch (Exception ex)
        {
            AppLog.Info("A second Timer window could not be activated. " + ex.GetType().Name);
        }
    }

    public static void StartListening(Action onActivate)
    {
        if (_event == null)
            return;

        _thread = new Thread(() =>
        {
            var handle = _event;
            if (handle == null)
                return;

            while (true)
            {
                try
                {
                    handle.WaitOne();
                    onActivate();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Single-instance listener stopped. " + ex.GetType().Name);
                    break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "Timer activation"
        };
        _thread.Start();
    }

    public static void Release()
    {
        var handle = _event;
        _event = null;
        handle?.Dispose();

        if (_mutex == null)
            return;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex.Dispose();
        _mutex = null;
    }
}

public sealed class TrayController : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly Action _open;
    private readonly Action _exit;
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly Icon _iconImage;
    private readonly bool _ownsIcon;
    private bool _disposed;

    public TrayController(MainViewModel viewModel, Action open, Action exit)
    {
        _viewModel = viewModel;
        _open = open;
        _exit = exit;
        (_iconImage, _ownsIcon) = LoadIcon();
        _menu = new ContextMenuStrip();
        _toggleItem = new ToolStripMenuItem("Start");
        _menu.Items.Add(new ToolStripMenuItem("Open Timer", null, (_, _) => _open()));
        _menu.Items.Add(_toggleItem);
        _menu.Items.Add(new ToolStripMenuItem("Reset", null, (_, _) => _viewModel.ResetCommand.Execute(null)));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => _exit()));
        _toggleItem.Click += (_, _) => _viewModel.ToggleCommand.Execute(null);

        _icon = new NotifyIcon
        {
            Icon = _iconImage,
            Visible = true,
            Text = "Timer",
            ContextMenuStrip = _menu
        };
        _icon.MouseUp += OnMouseUp;
        _viewModel.PropertyChanged += (_, _) => Sync();
        Sync();
    }

    public void ShowHintOnce()
    {
        if (!_viewModel.MarkTrayHintSeen())
            return;

        _icon.BalloonTipTitle = "Timer";
        _icon.BalloonTipText = "Timer keeps running in the notification area. Right-click the icon and choose Exit to quit.";
        _icon.ShowBalloonTip(4000);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        if (_ownsIcon)
            _iconImage.Dispose();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _open();
            return;
        }

        if (e.Button != MouseButtons.Right)
            return;

        Sync();
        var method = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(_icon, null);
    }

    private void Sync()
    {
        if (_disposed)
            return;

        var text = _viewModel.TrayText;
        if (text.Length > 63)
            text = text[..63];
        if (_icon.Text != text)
            _icon.Text = text;
        _toggleItem.Text = _viewModel.PrimaryActionLabel;
    }

    private static (Icon Icon, bool Owns) LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var associated = Icon.ExtractAssociatedIcon(path);
                if (associated != null)
                    return (associated, true);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not load the application icon. " + ex.GetType().Name);
        }

        return (SystemIcons.Application, false);
    }
}
