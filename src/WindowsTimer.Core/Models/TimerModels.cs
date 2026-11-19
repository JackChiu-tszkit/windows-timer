namespace WindowsTimer.Models;

public enum TimerStatus
{
    Idle,
    Running,
    Paused,
    Completed
}

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public enum AlertSoundId
{
    Bell,
    Chime,
    Soft
}

public enum CloseBehavior
{
    Exit,
    MinimizeToTray
}

public sealed class AppSettings
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public AlertSoundId AlertSound { get; set; } = AlertSoundId.Bell;
    public double Volume { get; set; } = 0.4;
    public bool Mute { get; set; }
    public bool RepeatUntilDismissed { get; set; }
    public bool AlwaysOnTop { get; set; }
    public CloseBehavior CloseBehavior { get; set; } = CloseBehavior.Exit;
    public bool LaunchOnStartup { get; set; }
    public bool HasSeenTrayHint { get; set; }
    public int LastHours { get; set; }
    public int LastMinutes { get; set; } = 25;
    public int LastSeconds { get; set; }
}
