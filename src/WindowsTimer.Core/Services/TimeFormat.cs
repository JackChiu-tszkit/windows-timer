namespace WindowsTimer.Services;

public static class TimeFormat
{
    public static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var seconds = remaining == TimeSpan.Zero
            ? 0
            : (long)Math.Ceiling(remaining.TotalSeconds - 1e-6);

        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        var secs = seconds % 60;
        return $"{hours:00}:{minutes:00}:{secs:00}";
    }

    public static string FormatParts(int hours, int minutes, int seconds) =>
        $"{Math.Clamp(hours, 0, 99):00}:{Math.Clamp(minutes, 0, 59):00}:{Math.Clamp(seconds, 0, 59):00}";
}
