using WindowsTimer.Models;

namespace WindowsTimer.Services;

/// <summary>
/// Countdown engine. Remaining time is derived from a monotonic clock,
/// not from counting UI ticks. Completion is raised at most once per run.
/// </summary>
public sealed class TimerEngine
{
    private readonly object _gate = new();
    private readonly IMonotonicClock _clock;
    private TimeSpan _duration;
    private TimeSpan _elapsedBeforeSegment;
    private long _segmentStart;
    private TimerStatus _status = TimerStatus.Idle;
    private bool _completionRaised;

    public TimerEngine(IMonotonicClock clock, TimeSpan? initialDuration = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        var duration = initialDuration ?? TimeSpan.FromMinutes(25);
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(initialDuration));
        _duration = duration > MaxDuration ? MaxDuration : duration;
    }

    public static TimeSpan MaxDuration { get; } = new(99, 59, 59);

    public event EventHandler? Completed;
    public event EventHandler? StateChanged;

    public TimerStatus Status
    {
        get { lock (_gate) return _status; }
    }

    public TimeSpan Duration
    {
        get { lock (_gate) return _duration; }
    }

    public TimeSpan Elapsed
    {
        get { lock (_gate) return ElapsedUnlocked(); }
    }

    public TimeSpan Remaining
    {
        get { lock (_gate) return RemainingUnlocked(); }
    }

    public bool SetDuration(TimeSpan duration)
    {
        Validate(duration);
        lock (_gate)
        {
            if (_status == TimerStatus.Running)
                return false;

            _duration = duration > MaxDuration ? MaxDuration : duration;
            _elapsedBeforeSegment = TimeSpan.Zero;
            _completionRaised = false;
            _status = TimerStatus.Idle;
        }

        OnStateChanged();
        return true;
    }

    public bool TryStart()
    {
        lock (_gate)
        {
            if (_status == TimerStatus.Running)
                return true;

            if (_duration <= TimeSpan.Zero)
                return false;

            if (_status is TimerStatus.Idle or TimerStatus.Completed)
            {
                _elapsedBeforeSegment = TimeSpan.Zero;
                _completionRaised = false;
            }

            _segmentStart = _clock.GetTimestamp();
            _status = TimerStatus.Running;
        }

        OnStateChanged();
        return true;
    }

    public bool StartDuration(TimeSpan duration)
    {
        Validate(duration);
        if (duration <= TimeSpan.Zero)
        {
            lock (_gate)
            {
                _duration = TimeSpan.Zero;
                _elapsedBeforeSegment = TimeSpan.Zero;
                _completionRaised = false;
                _status = TimerStatus.Idle;
            }

            OnStateChanged();
            return false;
        }

        lock (_gate)
        {
            _duration = duration > MaxDuration ? MaxDuration : duration;
            _elapsedBeforeSegment = TimeSpan.Zero;
            _completionRaised = false;
            _segmentStart = _clock.GetTimestamp();
            _status = TimerStatus.Running;
        }

        OnStateChanged();
        return true;
    }

    public void Pause()
    {
        var completed = false;
        var changed = false;
        lock (_gate)
        {
            if (_status != TimerStatus.Running)
                return;

            _elapsedBeforeSegment += _clock.ElapsedSince(_segmentStart);
            if (_elapsedBeforeSegment >= _duration)
            {
                completed = CompleteUnlocked();
            }
            else
            {
                _status = TimerStatus.Paused;
                changed = true;
            }
        }

        if (completed)
            OnCompleted();
        else if (changed)
            OnStateChanged();
    }

    public void Reset()
    {
        lock (_gate)
        {
            _elapsedBeforeSegment = TimeSpan.Zero;
            _completionRaised = false;
            _status = TimerStatus.Idle;
        }

        OnStateChanged();
    }

    public void Cancel() => Reset();

    public void Tick()
    {
        var completed = false;
        lock (_gate)
        {
            if (_status == TimerStatus.Running && ElapsedUnlocked() >= _duration)
                completed = CompleteUnlocked();
        }

        if (completed)
            OnCompleted();
    }

    private static void Validate(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
    }

    private TimeSpan ElapsedUnlocked()
    {
        var elapsed = _elapsedBeforeSegment;
        if (_status == TimerStatus.Running)
            elapsed += _clock.ElapsedSince(_segmentStart);

        if (elapsed < TimeSpan.Zero)
            return TimeSpan.Zero;
        if (elapsed > _duration)
            return _duration;
        return elapsed;
    }

    private TimeSpan RemainingUnlocked()
    {
        var remaining = _duration - ElapsedUnlocked();
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private bool CompleteUnlocked()
    {
        if (_completionRaised)
            return false;

        _elapsedBeforeSegment = _duration;
        _status = TimerStatus.Completed;
        _completionRaised = true;
        return true;
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void OnCompleted()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
