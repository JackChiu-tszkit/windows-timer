namespace WindowsTimer.Services;

/// <summary>
/// Sleep and resume policy for the countdown.
/// The timer measures progress with a monotonic clock (Stopwatch /
/// QueryPerformanceCounter). That clock stops while Windows is suspended,
/// so system sleep pauses the countdown. On resume, the timer continues
/// from the remaining time at the moment sleep began. Wall-clock time spent
/// asleep is not subtracted and is not added.
/// </summary>
public static class SleepPolicy
{
    public const string Description =
        "System sleep pauses the countdown. The timer uses a monotonic clock that does not advance while Windows is suspended, then continues from the remaining time on resume.";

    public readonly record struct SleepGap(TimeSpan MonotonicElapsed, TimeSpan WallClockElapsed);

    public static TimeSpan CountedElapsed(SleepGap gap)
    {
        var elapsed = gap.MonotonicElapsed;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    public static TimeSpan RemainingAfterResume(TimeSpan remainingBeforeSleep, TimeSpan monotonicElapsedDuringSleep)
    {
        if (monotonicElapsedDuringSleep < TimeSpan.Zero)
            monotonicElapsedDuringSleep = TimeSpan.Zero;

        var remaining = remainingBeforeSleep - monotonicElapsedDuringSleep;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }
}
