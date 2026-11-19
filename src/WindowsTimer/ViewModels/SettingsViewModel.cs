using System.Windows;
using System.Windows.Input;
using WindowsTimer.Models;
using WindowsTimer.Services;

namespace WindowsTimer.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly AlarmPlayer _alarm;
    private readonly StartupService _startup;
    private readonly Action _appearanceChanged;

    public SettingsViewModel(
        AppSettings settings,
        SettingsService settingsService,
        ThemeService themeService,
        AlarmPlayer alarm,
        StartupService startup,
        Action appearanceChanged)
    {
        Settings = settings;
        _settingsService = settingsService;
        _themeService = themeService;
        _alarm = alarm;
        _startup = startup;
        _appearanceChanged = appearanceChanged;
        PreviewCommand = new RelayCommand(() => _alarm.Preview(Settings));
    }

    public AppSettings Settings { get; }

    public ICommand PreviewCommand { get; }

    public bool IsThemeSystem
    {
        get => Settings.Theme == ThemePreference.System;
        set { if (value) SetTheme(ThemePreference.System); }
    }

    public bool IsThemeLight
    {
        get => Settings.Theme == ThemePreference.Light;
        set { if (value) SetTheme(ThemePreference.Light); }
    }

    public bool IsThemeDark
    {
        get => Settings.Theme == ThemePreference.Dark;
        set { if (value) SetTheme(ThemePreference.Dark); }
    }

    public bool IsSoundBell
    {
        get => Settings.AlertSound == AlertSoundId.Bell;
        set { if (value) SetSound(AlertSoundId.Bell); }
    }

    public bool IsSoundChime
    {
        get => Settings.AlertSound == AlertSoundId.Chime;
        set { if (value) SetSound(AlertSoundId.Chime); }
    }

    public bool IsSoundSoft
    {
        get => Settings.AlertSound == AlertSoundId.Soft;
        set { if (value) SetSound(AlertSoundId.Soft); }
    }

    public bool CloseMinimizes
    {
        get => Settings.CloseBehavior == CloseBehavior.MinimizeToTray;
        set { if (value) SetCloseBehavior(CloseBehavior.MinimizeToTray); }
    }

    public bool CloseExits
    {
        get => Settings.CloseBehavior == CloseBehavior.Exit;
        set { if (value) SetCloseBehavior(CloseBehavior.Exit); }
    }

    public bool IsMuted
    {
        get => Settings.Mute;
        set
        {
            if (Settings.Mute == value)
                return;
            Settings.Mute = value;
            Save();
            OnPropertyChanged();
        }
    }

    public bool RepeatUntilDismissed
    {
        get => Settings.RepeatUntilDismissed;
        set
        {
            if (Settings.RepeatUntilDismissed == value)
                return;
            Settings.RepeatUntilDismissed = value;
            Save();
            OnPropertyChanged();
        }
    }

    public bool AlwaysOnTop
    {
        get => Settings.AlwaysOnTop;
        set
        {
            if (Settings.AlwaysOnTop == value)
                return;
            Settings.AlwaysOnTop = value;
            Save();
            OnPropertyChanged();
            _appearanceChanged();
        }
    }

    public bool LaunchOnStartup
    {
        get => Settings.LaunchOnStartup;
        set
        {
            if (Settings.LaunchOnStartup == value)
                return;

            try
            {
                _startup.SetEnabled(value);
                Settings.LaunchOnStartup = value;
                Save();
                OnPropertyChanged();
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not update the Windows startup entry", ex);
                MessageBox.Show(
                    "Timer could not change the Windows startup setting.",
                    "Timer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                OnPropertyChanged();
            }
        }
    }

    public double VolumePercent
    {
        get => Math.Round(Settings.Volume * 100);
        set
        {
            var volume = Math.Clamp(value, 0, 100) / 100d;
            if (Math.Abs(Settings.Volume - volume) < 0.0001)
                return;
            Settings.Volume = volume;
            Save();
            OnPropertyChanged();
        }
    }

    private void SetTheme(ThemePreference theme)
    {
        if (Settings.Theme == theme)
            return;
        Settings.Theme = theme;
        _themeService.Apply(theme);
        Save();
        NotifyTheme();
    }

    private void SetSound(AlertSoundId sound)
    {
        if (Settings.AlertSound == sound)
            return;
        Settings.AlertSound = sound;
        Save();
        OnPropertyChanged(nameof(IsSoundBell));
        OnPropertyChanged(nameof(IsSoundChime));
        OnPropertyChanged(nameof(IsSoundSoft));
    }

    private void SetCloseBehavior(CloseBehavior behavior)
    {
        if (Settings.CloseBehavior == behavior)
            return;
        Settings.CloseBehavior = behavior;
        Save();
        OnPropertyChanged(nameof(CloseMinimizes));
        OnPropertyChanged(nameof(CloseExits));
    }

    private void NotifyTheme()
    {
        OnPropertyChanged(nameof(IsThemeSystem));
        OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(IsThemeDark));
    }

    private void Save() => _settingsService.Save(Settings);
}
