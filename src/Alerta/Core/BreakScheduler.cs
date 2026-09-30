namespace Alerta.Core;

public sealed class BreakScheduler
{
    private readonly IClock _clock;
    private readonly IIdleSource _idle;
    private readonly IBusySource _busy;
    private Settings _settings;

    private Phase _phase = Phase.Working;
    private TimeSpan _worked = TimeSpan.Zero;
    private DateTime _lastTick;
    private DateTime _dueSince;
    private DateTime _nextNotifyAt;
    private DateTime _breakEndsAt;
    private DateTime _suspendedUntil;
    private DateTime _nextBusyCheck = DateTime.MinValue;
    private bool _lastBusy;
    private int _notifySequence;

    public BreakScheduler(Settings settings, IClock clock, IIdleSource idle, IBusySource busy)
    {
        _settings = settings.Normalized();
        _clock = clock;
        _idle = idle;
        _busy = busy;
        _lastTick = clock.Now;
    }

    public void ApplySettings(Settings settings) => _settings = settings.Normalized();

    public SchedulerSnapshot Tick()
    {
        var now = _clock.Now;
        var delta = now - _lastTick;
        if (delta < TimeSpan.Zero)
        {
            ShiftDeadlines(delta);
            delta = TimeSpan.Zero;
        }
        _lastTick = now;

        switch (_phase)
        {
            case Phase.Suspended:
                if (now >= _suspendedUntil) ResetCycle();
                break;

            case Phase.OnBreak:
                if (now >= _breakEndsAt) ResetCycle();
                break;

            case Phase.Working:
                if (RestedSinceLastTick(now, delta))
                {
                    ResetCycle();
                    break;
                }
                _worked += delta;
                if (_worked >= _settings.Work && !IsBusy(now)) EnterDue(now);
                break;

            case Phase.Due:
                if (RestedSinceLastTick(now, delta))
                {
                    ResetCycle();
                    break;
                }
                if (StageAt(now) == 3)
                {
                    if (now >= LockEndsAt) ResetCycle();
                    break;
                }
                if (IsBusy(now))
                {
                    _phase = Phase.Working;
                    break;
                }
                if (_settings.Mode == AlertMode.NotifyOnly && now >= _nextNotifyAt)
                {
                    _notifySequence++;
                    _nextNotifyAt = now + _settings.NotifyRepeat;
                }
                break;
        }

        return Snapshot(now);
    }

    public void StartBreak()
    {
        _phase = Phase.OnBreak;
        _breakEndsAt = _clock.Now + _settings.Break;
    }

    /// <summary>Working: pushes the break back by <paramref name="by"/>. Otherwise: next break in <paramref name="by"/>.</summary>
    public void Snooze(TimeSpan by)
    {
        _worked = _phase == Phase.Working
            ? (_worked < _settings.Work ? _worked : _settings.Work) - by
            : _settings.Work - by;
        _phase = Phase.Working;
    }

    public void Skip() => ResetCycle();

    public void Suspend(DateTime untilUtc)
    {
        _phase = Phase.Suspended;
        _suspendedUntil = untilUtc;
    }

    public void Resume() => ResetCycle();

    private DateTime LockEndsAt => _dueSince + _settings.Stage2After + _settings.Stage3After + _settings.Lock;

    /// <summary>A long tick gap (sleep) or real inactivity outside a meeting counts as a break.</summary>
    private bool RestedSinceLastTick(DateTime now, TimeSpan delta) =>
        delta >= _settings.IdleReset || (_idle.IdleTime >= _settings.IdleReset && !IsBusy(now));

    private bool IsBusy(DateTime now)
    {
        if (now >= _nextBusyCheck)
        {
            _lastBusy = _busy.IsBusy();
            _nextBusyCheck = now + _settings.BusyRecheck;
        }
        return _lastBusy;
    }

    /// <summary>The wall clock moved backwards: keep running countdowns relative to real elapsed time.</summary>
    private void ShiftDeadlines(TimeSpan by)
    {
        if (_phase == Phase.Due)
        {
            _dueSince += by;
            _nextNotifyAt += by;
        }
        if (_phase == Phase.OnBreak) _breakEndsAt += by;
        _nextBusyCheck = DateTime.MinValue;
    }

    private void EnterDue(DateTime now)
    {
        _phase = Phase.Due;
        _dueSince = now;
        _notifySequence++;
        _nextNotifyAt = now + _settings.NotifyRepeat;
    }

    private void ResetCycle()
    {
        _phase = Phase.Working;
        _worked = TimeSpan.Zero;
        _lastTick = _clock.Now;
    }

    private int StageAt(DateTime now)
    {
        if (_settings.Mode == AlertMode.NotifyOnly) return 1;
        var elapsed = now - _dueSince;
        if (elapsed >= _settings.Stage2After + _settings.Stage3After) return 3;
        if (elapsed >= _settings.Stage2After) return 2;
        return 1;
    }

    private SchedulerSnapshot Snapshot(DateTime now)
    {
        var work = _settings.Work;
        var progress = Math.Clamp(_worked / work, 0.0, 1.0);
        var untilBreak = Max0(work - _worked);
        var stage = _phase == Phase.Due ? StageAt(now) : 0;

        return new SchedulerSnapshot(
            Phase: _phase,
            Stage: stage,
            NotifySequence: _notifySequence,
            Progress: progress,
            UntilBreak: untilBreak,
            BreakRemaining: _phase == Phase.OnBreak ? Max0(_breakEndsAt - now) : TimeSpan.Zero,
            LockRemaining: stage == 3 ? Max0(LockEndsAt - now) : TimeSpan.Zero,
            DeferredByBusy: _phase == Phase.Working && _worked >= work,
            SuspendedUntil: _phase == Phase.Suspended ? _suspendedUntil : null);
    }

    private static TimeSpan Max0(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;
}
