using System.Diagnostics;
using WindowsTimer.Services;

namespace WindowsTimer.Tests;

internal sealed class FakeClock : IMonotonicClock
{
    private long _timestamp;

    public long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan time)
    {
        var delta = (long)Math.Round(time.Ticks * (Stopwatch.Frequency / (double)TimeSpan.TicksPerSecond));
        if (delta <= 0 && time > TimeSpan.Zero)
            delta = 1;
        _timestamp += delta;
    }

    public TimeSpan ElapsedSince(long timestamp) => Stopwatch.GetElapsedTime(timestamp, _timestamp);
}
