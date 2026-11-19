using System.Windows.Input;
using WindowsTimer.Models;
using WindowsTimer.Services;

namespace WindowsTimer.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly TimerEngine _engine;
    private readonly SettingsService _settingsService;
    private string _hoursText;
    private string _minutesText;
    private string _secondsText;
    private int _hours;
    private int _minutes;
    private int _seconds;
    private int? _selectedPreset;
    private bool _isMiniMode;
    private string _publishedDisplay = string.Empty;
    private TimerStatus _publishedStatus = TimerStatus.Idle;

    public MainViewModel(TimerEngine engine, AppSettings settings, SettingsService settingsService)
    {
        _engine = engine;
        Settings = settings;
        _settingsService = settingsService;
        _hours = Math.Clamp(settings.LastHours, 0, 99);
        _minutes = Math.Clamp(settings.LastMinutes, 0, 59);
        _seconds = Math.Clamp(settings.LastSeconds, 0, 59);
        _hoursText = _hours.ToString("00");
        _minutesText = _minutes.ToString("00");
        _secondsText = _seconds.ToString("00");
        _selectedPreset = MatchPreset();

        ToggleCommand = new RelayCommand(Toggle, () => CanToggle);
        ResetCommand = new RelayCommand(Reset);
        SelectPresetCommand = new RelayCommand(SelectPreset);
        ToggleMiniCommand = new RelayCommand(() => IsMiniMode = !IsMiniMode);
        ToggleTopmostCommand = new RelayCommand(() => AlwaysOnTop = !AlwaysOnTop);
        OpenSettingsCommand = new RelayCommand(() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));

        _engine.Completed += (_, _) =>
        {
            Publish();
            Completed?.Invoke(this, EventArgs.Empty);
        };
    }

    public AppSettings Settings { get; }

    public IReadOnlyList<PresetOption> Presets { get; } =
    [
        new(1, "1m", "1 minute"),
        new(5, "5m", "5 minutes"),
        new(10, "10m", "10 minutes"),
        new(15, "15m", "15 minutes"),
        new(25, "25m", "25 minutes"),
        new(30, "30m", "30 minutes"),
        new(60, "60m", "60 minutes")
    ];

    public ICommand ToggleCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SelectPresetCommand { get; }
    public ICommand ToggleMiniCommand { get; }
    public ICommand ToggleTopmostCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public event EventHandler? Completed;
    public event EventHandler? OpenSettingsRequested;

    public string HoursText
    {
        get => _hoursText;
        set => SetPart(value, 99, ref _hoursText, ref _hours, nameof(HoursText));
    }

    public string MinutesText
    {
        get => _minutesText;
        set => SetPart(value, 59, ref _minutesText, ref _minutes, nameof(MinutesText));
    }

    public string SecondsText
    {
        get => _secondsText;
        set => SetPart(value, 59, ref _secondsText, ref _seconds, nameof(SecondsText));
    }

    public string DisplayTime => TimeFormat.FormatRemaining(_engine.Remaining);

    public string WindowTitle =>
        _engine.Status == TimerStatus.Running ? "Timer " + DisplayTime : "Timer";

    public string TrayText => _engine.Status switch
    {
        TimerStatus.Running => "Timer " + DisplayTime,
        TimerStatus.Paused => "Timer paused",
        TimerStatus.Completed => "Time's up",
        _ => "Timer"
    };

    public string StatusText => _engine.Status switch
    {
        TimerStatus.Running => "Running",
        TimerStatus.Paused => "Paused",
        TimerStatus.Completed => "Time's up",
        _ => "Ready"
    };

    public string PrimaryActionLabel => _engine.Status switch
    {
        TimerStatus.Running => "Pause",
        TimerStatus.Paused => "Resume",
        _ => "Start"
    };

    public string PrimaryGlyph => IsRunning ? "\uE769" : "\uE768";

    public bool IsRunning => _engine.Status == TimerStatus.Running;

    public bool IsPaused => _engine.Status == TimerStatus.Paused;

    public bool CanEditTime => _engine.Status == TimerStatus.Idle;

    public bool CanToggle => IsRunning || _engine.Duration > TimeSpan.Zero;

    public double RingProgress
    {
        get
        {
            var duration = _engine.Duration.TotalSeconds;
            if (duration <= 0)
                return 0;
            return Math.Clamp(_engine.Remaining.TotalSeconds / duration, 0, 1);
        }
    }

    public int? SelectedPresetMinutes
    {
        get => _selectedPreset;
        private set
        {
            if (_selectedPreset == value)
                return;
            _selectedPreset = value;
            OnPropertyChanged();
        }
    }

    public bool IsMiniMode
    {
        get => _isMiniMode;
        set
        {
            if (_isMiniMode == value)
                return;
            _isMiniMode = value;
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
            _settingsService.Save(Settings);
            OnPropertyChanged();
        }
    }

    public void Tick()
    {
        _engine.Tick();
        if (DisplayTime == _publishedDisplay && _engine.Status == _publishedStatus)
        {
            if (_engine.Status == TimerStatus.Running)
                OnPropertyChanged(nameof(RingProgress));
            return;
        }

        Publish();
    }

    public void NotifyAppearance() => OnPropertyChanged(nameof(AlwaysOnTop));

    public bool MarkTrayHintSeen()
    {
        if (Settings.HasSeenTrayHint)
            return false;

        Settings.HasSeenTrayHint = true;
        _settingsService.Save(Settings);
        return true;
    }

    public void NormalizeTimeText()
    {
        _hoursText = _hours.ToString("00");
        _minutesText = _minutes.ToString("00");
        _secondsText = _seconds.ToString("00");
        OnPropertyChanged(nameof(HoursText));
        OnPropertyChanged(nameof(MinutesText));
        OnPropertyChanged(nameof(SecondsText));
    }

    private void Toggle()
    {
        if (_engine.Status == TimerStatus.Running)
        {
            _engine.Pause();
            Publish();
            return;
        }

        if (_engine.Status == TimerStatus.Paused)
        {
            _engine.TryStart();
            Publish();
            return;
        }

        NormalizeTimeText();
        _engine.SetDuration(new TimeSpan(_hours, _minutes, _seconds));
        SaveLastDuration();
        if (!_engine.TryStart())
            return;

        Publish();
    }

    private void Reset()
    {
        _engine.Reset();
        NormalizeTimeText();
        SelectedPresetMinutes = MatchPreset();
        Publish();
    }

    private void SelectPreset(object? parameter)
    {
        if (!TryReadMinutes(parameter, out var minutes))
            return;

        var duration = TimeSpan.FromMinutes(minutes);
        _hours = (int)duration.TotalHours;
        _minutes = duration.Minutes;
        _seconds = 0;
        NormalizeTimeText();
        _engine.StartDuration(duration);
        SelectedPresetMinutes = minutes;
        SaveLastDuration();
        Publish();
    }

    private void SetPart(string? value, int max, ref string text, ref int number, string propertyName)
    {
        if (_engine.Status != TimerStatus.Idle)
            return;

        var digits = new string((value ?? string.Empty).Where(char.IsDigit).Take(2).ToArray());
        int parsed;
        string display;
        if (digits.Length == 0)
        {
            parsed = 0;
            display = string.Empty;
        }
        else
        {
            parsed = int.Parse(digits);
            if (parsed > max)
            {
                parsed = max;
                display = max.ToString();
            }
            else
            {
                display = digits;
            }
        }

        if (text == display && number == parsed)
            return;

        text = display;
        number = parsed;
        OnPropertyChanged(propertyName);
        _engine.SetDuration(new TimeSpan(_hours, _minutes, _seconds));
        SelectedPresetMinutes = MatchPreset();
        SaveLastDuration();
        Publish();
    }

    private void Publish()
    {
        _publishedDisplay = DisplayTime;
        _publishedStatus = _engine.Status;
        OnPropertyChanged(nameof(DisplayTime));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(TrayText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(PrimaryGlyph));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(CanEditTime));
        OnPropertyChanged(nameof(CanToggle));
        OnPropertyChanged(nameof(RingProgress));
        OnPropertyChanged(nameof(SelectedPresetMinutes));
        CommandManager.InvalidateRequerySuggested();
    }

    private void SaveLastDuration()
    {
        if (Settings.LastHours == _hours && Settings.LastMinutes == _minutes && Settings.LastSeconds == _seconds)
            return;

        Settings.LastHours = _hours;
        Settings.LastMinutes = _minutes;
        Settings.LastSeconds = _seconds;
        _settingsService.Save(Settings);
    }

    private int? MatchPreset()
    {
        var total = (_hours * 3600) + (_minutes * 60) + _seconds;
        foreach (var preset in Presets)
        {
            if (preset.Minutes * 60 == total)
                return preset.Minutes;
        }

        return null;
    }

    private static bool TryReadMinutes(object? parameter, out int minutes)
    {
        switch (parameter)
        {
            case int value:
                minutes = value;
                break;
            case string text when int.TryParse(text, out var parsed):
                minutes = parsed;
                break;
            default:
                minutes = 0;
                return false;
        }

        return minutes is > 0 and <= 99 * 60;
    }
}
