namespace WindowsTimer.Services;

/// <summary>
/// Monotonic clock. Implementations must not move backwards and must not
/// depend on the wall clock.
/// </summary>
public interface IMonotonicClock
{
    long GetTimestamp();
    TimeSpan ElapsedSince(long timestamp);
}

public sealed class StopwatchClock : IMonotonicClock
{
    public long GetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();

    public TimeSpan ElapsedSince(long timestamp) =>
        System.Diagnostics.Stopwatch.GetElapsedTime(timestamp);
}
