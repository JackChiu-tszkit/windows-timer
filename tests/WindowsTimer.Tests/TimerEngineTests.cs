using WindowsTimer.Models;
using WindowsTimer.Services;
using Xunit;

namespace WindowsTimer.Tests;

public class TimerEngineTests
{
    [Fact]
    public void NewTimer_UsesInitialDurationAndIdleState()
    {
        var engine = new TimerEngine(new FakeClock(), TimeSpan.FromMinutes(25));

        Assert.Equal(TimerStatus.Idle, engine.Status);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.Duration);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.Remaining);
        Assert.Equal(TimeSpan.Zero, engine.Elapsed);
    }

    [Fact]
    public void StartPauseResume_TracksOnlyRunningTime()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromSeconds(60));

        Assert.True(engine.TryStart());
        clock.Advance(TimeSpan.FromSeconds(10));
        engine.Pause();
        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(TimerStatus.Paused, engine.Status);
        Near(TimeSpan.FromSeconds(50), engine.Remaining);

        Assert.True(engine.TryStart());
        clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimerStatus.Running, engine.Status);
        Near(TimeSpan.FromSeconds(45), engine.Remaining);
    }

    [Fact]
    public void Reset_ClearsElapsedWithoutChangingDuration()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromMinutes(10));
        engine.TryStart();
        clock.Advance(TimeSpan.FromMinutes(3));

        engine.Reset();
        clock.Advance(TimeSpan.FromMinutes(4));

        Assert.Equal(TimerStatus.Idle, engine.Status);
        Assert.Equal(TimeSpan.FromMinutes(10), engine.Remaining);
        Assert.Equal(TimeSpan.Zero, engine.Elapsed);
    }

    [Fact]
    public void Cancel_MatchesReset()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromSeconds(30));
        engine.TryStart();
        clock.Advance(TimeSpan.FromSeconds(8));

        engine.Cancel();

        Assert.Equal(TimerStatus.Idle, engine.Status);
        Assert.Equal(TimeSpan.FromSeconds(30), engine.Remaining);
    }

    [Fact]
    public void ZeroDuration_DoesNotStartOrComplete()
    {
        var engine = new TimerEngine(new FakeClock(), TimeSpan.Zero);
        var completions = 0;
        engine.Completed += (_, _) => completions++;

        Assert.False(engine.TryStart());
        engine.Tick();

        Assert.Equal(TimerStatus.Idle, engine.Status);
        Assert.Equal(0, completions);
        Assert.False(engine.StartDuration(TimeSpan.Zero));
        Assert.Equal(0, completions);
    }

    [Fact]
    public void Completion_FiresExactlyOnce()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromSeconds(5));
        var completions = 0;
        engine.Completed += (_, _) => completions++;

        engine.TryStart();
        clock.Advance(TimeSpan.FromSeconds(5));
        engine.Tick();
        clock.Advance(TimeSpan.FromSeconds(10));
        for (var i = 0; i < 20; i++)
            engine.Tick();

        Assert.Equal(1, completions);
        Assert.Equal(TimerStatus.Completed, engine.Status);
        Assert.Equal(TimeSpan.Zero, engine.Remaining);
    }

    [Fact]
    public void RepeatedStart_DoesNotCreateASecondRunOrResetProgress()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromSeconds(30));
        var completions = 0;
        engine.Completed += (_, _) => completions++;

        Assert.True(engine.TryStart());
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.True(engine.TryStart());
        Assert.True(engine.TryStart());
        clock.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal(TimerStatus.Running, engine.Status);
        Near(TimeSpan.FromSeconds(8), engine.Elapsed);
        Assert.Equal(0, completions);
    }

    [Fact]
    public void RepeatedPauseAndReset_AreStable()
    {
        var engine = new TimerEngine(new FakeClock(), TimeSpan.FromSeconds(15));
        engine.Pause();
        engine.Pause();
        engine.Reset();
        engine.Reset();
        engine.Cancel();

        Assert.Equal(TimerStatus.Idle, engine.Status);
        Assert.Equal(TimeSpan.FromSeconds(15), engine.Remaining);
    }

    [Fact]
    public void Accuracy_LongCountdownUsesElapsedTime()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromHours(99));
        engine.TryStart();
        clock.Advance(TimeSpan.FromHours(2));

        Near(TimeSpan.FromHours(97), engine.Remaining);
    }

    [Fact]
    public void VeryShortDuration_CompletesOnce()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromMilliseconds(1));
        var completions = 0;
        engine.Completed += (_, _) => completions++;

        engine.TryStart();
        clock.Advance(TimeSpan.FromMilliseconds(2));
        engine.Tick();
        engine.Tick();

        Assert.Equal(1, completions);
        Assert.Equal(TimerStatus.Completed, engine.Status);
    }

    [Fact]
    public void PauseAtExactEnd_CompletesOnce()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromSeconds(3));
        var completions = 0;
        engine.Completed += (_, _) => completions++;
        engine.TryStart();
        clock.Advance(TimeSpan.FromSeconds(3));

        engine.Pause();
        engine.Pause();
        engine.Tick();

        Assert.Equal(1, completions);
        Assert.Equal(TimerStatus.Completed, engine.Status);
    }

    [Fact]
    public void NegativeDuration_IsRejected()
    {
        var engine = new TimerEngine(new FakeClock());
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.SetDuration(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void SetDuration_WhileRunning_DoesNotChangeTheActiveTimer()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromMinutes(10));
        engine.TryStart();
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.False(engine.SetDuration(TimeSpan.FromMinutes(2)));

        Assert.Equal(TimerStatus.Running, engine.Status);
        Assert.Equal(TimeSpan.FromMinutes(10), engine.Duration);
    }

    [Fact]
    public void DurationAboveMaximum_IsClamped()
    {
        var engine = new TimerEngine(new FakeClock());
        Assert.True(engine.SetDuration(TimeSpan.FromHours(200)));
        Assert.Equal(TimerEngine.MaxDuration, engine.Duration);
    }

    [Fact]
    public void SleepDoesNotConsumeRemainingTime()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromMinutes(25));
        engine.TryStart();
        clock.Advance(TimeSpan.FromMinutes(5));
        var beforeSleep = engine.Remaining;

        var counted = SleepPolicy.CountedElapsed(new SleepPolicy.SleepGap(
            MonotonicElapsed: TimeSpan.Zero,
            WallClockElapsed: TimeSpan.FromMinutes(30)));

        Assert.Equal(TimeSpan.Zero, counted);
        Assert.Equal(beforeSleep, engine.Remaining);
        Assert.Equal(beforeSleep, SleepPolicy.RemainingAfterResume(beforeSleep, counted));
    }

    [Fact]
    public void RestartAfterCompletion_RunsAgainAndCanCompleteOnceMore()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock, TimeSpan.FromSeconds(2));
        var completions = 0;
        engine.Completed += (_, _) => completions++;
        engine.TryStart();
        clock.Advance(TimeSpan.FromSeconds(2));
        engine.Tick();

        Assert.True(engine.TryStart());
        Assert.Equal(TimerStatus.Running, engine.Status);
        Near(TimeSpan.FromSeconds(2), engine.Remaining);

        clock.Advance(TimeSpan.FromSeconds(2));
        engine.Tick();

        Assert.Equal(2, completions);
    }

    private static void Near(TimeSpan expected, TimeSpan actual)
    {
        var delta = (expected - actual).Duration();
        Assert.True(delta <= TimeSpan.FromMilliseconds(20), $"Expected {expected} but was {actual}.");
    }
}
