using Alerta.Core;

namespace Alerta.Tests;

public class BreakSchedulerTests
{
    private static readonly Settings Fast = new()
    {
        WorkSeconds = 60,
        BreakSeconds = 30,
        IdleResetSeconds = 45,
        Stage2AfterSeconds = 20,
        Stage3AfterSeconds = 20,
        LockSeconds = 10,
        NotifyRepeatSeconds = 30,
        BusyRecheckSeconds = 5,
    };

    private readonly FakeClock _clock = new();
    private readonly FakeIdle _idle = new();
    private readonly FakeBusy _busy = new();

    private BreakScheduler Create(Settings? settings = null) => new(settings ?? Fast, _clock, _idle, _busy);

    /// <summary>Advances the clock one second at a time, ticking after each step.</summary>
    private SchedulerSnapshot Run(BreakScheduler scheduler, int seconds)
    {
        var last = scheduler.Tick();
        for (var i = 0; i < seconds; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            last = scheduler.Tick();
        }
        return last;
    }

    [Fact]
    public void Starts_working_with_full_interval_remaining()
    {
        var snap = Create().Tick();

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Stage);
        Assert.Equal(0, snap.Progress);
        Assert.Equal(TimeSpan.FromSeconds(60), snap.UntilBreak);
    }

    [Fact]
    public void Becomes_due_when_work_interval_elapses()
    {
        var s = Create();

        Assert.Equal(Phase.Working, Run(s, 59).Phase);
        var snap = Run(s, 1);

        Assert.Equal(Phase.Due, snap.Phase);
        Assert.Equal(1, snap.Stage);
        Assert.Equal(1, snap.NotifySequence);
        Assert.Equal(1.0, snap.Progress);
        Assert.Equal(TimeSpan.Zero, snap.UntilBreak);
    }

    [Fact]
    public void Escalates_to_dim_then_lock_then_restarts_cycle()
    {
        var s = Create();
        Run(s, 60);

        Assert.Equal(1, Run(s, 19).Stage);
        Assert.Equal(2, Run(s, 1).Stage);

        var locked = Run(s, 20);
        Assert.Equal(3, locked.Stage);
        Assert.Equal(TimeSpan.FromSeconds(10), locked.LockRemaining);

        var after = Run(s, 10);
        Assert.Equal(Phase.Working, after.Phase);
        Assert.Equal(0, after.Progress);
    }

    [Fact]
    public void Notify_only_mode_never_escalates_and_repeats_notification()
    {
        var s = Create(Fast with { Mode = AlertMode.NotifyOnly });
        Run(s, 60);

        var snap = Run(s, 29);
        Assert.Equal(1, snap.Stage);
        Assert.Equal(1, snap.NotifySequence);

        Assert.Equal(2, Run(s, 1).NotifySequence);

        snap = Run(s, 30);
        Assert.Equal(3, snap.NotifySequence);
        Assert.Equal(1, snap.Stage);
        Assert.Equal(Phase.Due, snap.Phase);
    }

    [Fact]
    public void Idle_beyond_threshold_counts_as_break()
    {
        var s = Create();
        Run(s, 30);

        _idle.IdleTime = TimeSpan.FromSeconds(44);
        Assert.True(Run(s, 1).Progress > 0.5);

        _idle.IdleTime = TimeSpan.FromSeconds(45);
        var snap = Run(s, 1);
        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Idle_during_due_counts_as_break()
    {
        var s = Create();
        Run(s, 60);
        _idle.IdleTime = TimeSpan.FromSeconds(45);

        var snap = Run(s, 1);

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Idle_while_busy_does_not_count_as_break()
    {
        _busy.Busy = true;
        var s = Create();
        Run(s, 10);
        _idle.IdleTime = TimeSpan.FromSeconds(100);

        var snap = Run(s, 10);

        Assert.True(snap.Progress > 0.3);
    }

    [Fact]
    public void Clock_jump_longer_than_idle_threshold_counts_as_break()
    {
        var s = Create();
        Run(s, 30);
        _clock.Advance(TimeSpan.FromMinutes(10));

        var snap = s.Tick();

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Clock_moving_backwards_is_ignored()
    {
        var s = Create();
        Run(s, 30);
        _clock.Advance(TimeSpan.FromSeconds(-20));

        var snap = s.Tick();

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0.5, snap.Progress, 3);
    }

    [Fact]
    public void Busy_defers_due_until_busy_ends()
    {
        _busy.Busy = true;
        var s = Create();

        var snap = Run(s, 60);
        Assert.Equal(Phase.Working, snap.Phase);
        Assert.True(snap.DeferredByBusy);

        _busy.Busy = false;
        snap = Run(s, 5);
        Assert.Equal(Phase.Due, snap.Phase);
        Assert.False(snap.DeferredByBusy);
    }

    [Fact]
    public void Meeting_starting_during_toast_pulls_alert_back()
    {
        var s = Create();
        Run(s, 60);
        _busy.Busy = true;

        var snap = Run(s, 5);

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.True(snap.DeferredByBusy);
    }

    [Fact]
    public void Busy_source_is_not_polled_while_simply_working()
    {
        var s = Create();

        Run(s, 59);

        Assert.Equal(0, _busy.Calls);
    }

    [Fact]
    public void Start_break_counts_down_then_restarts_cycle()
    {
        var s = Create();
        Run(s, 60);
        s.StartBreak();

        var snap = s.Tick();
        Assert.Equal(Phase.OnBreak, snap.Phase);
        Assert.Equal(TimeSpan.FromSeconds(30), snap.BreakRemaining);

        snap = Run(s, 30);
        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Snooze_while_due_sets_remaining_to_snooze_length()
    {
        var s = Create();
        Run(s, 60);
        s.Snooze(TimeSpan.FromSeconds(15));

        var snap = s.Tick();
        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(TimeSpan.FromSeconds(15), snap.UntilBreak);

        Assert.Equal(Phase.Due, Run(s, 15).Phase);
    }

    [Fact]
    public void Snooze_while_working_pushes_break_back()
    {
        var s = Create();
        Run(s, 40);
        s.Snooze(TimeSpan.FromSeconds(15));

        Assert.Equal(TimeSpan.FromSeconds(35), s.Tick().UntilBreak);
    }

    [Fact]
    public void Snooze_longer_than_worked_time_postpones_past_full_interval()
    {
        var s = Create();
        Run(s, 10);
        s.Snooze(TimeSpan.FromSeconds(30));

        var snap = s.Tick();

        Assert.Equal(TimeSpan.FromSeconds(80), snap.UntilBreak);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Skip_restarts_cycle()
    {
        var s = Create();
        Run(s, 60);
        s.Skip();

        var snap = s.Tick();

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Suspend_ignores_time_until_deadline()
    {
        var s = Create();
        var until = _clock.Now.AddSeconds(100);
        s.Suspend(until);

        var snap = Run(s, 99);
        Assert.Equal(Phase.Suspended, snap.Phase);
        Assert.Equal(until, snap.SuspendedUntil);

        snap = Run(s, 1);
        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
        Assert.Null(snap.SuspendedUntil);
    }

    [Fact]
    public void Resume_ends_suspension()
    {
        var s = Create();
        s.Suspend(_clock.Now.AddHours(1));
        s.Resume();

        Assert.Equal(Phase.Working, s.Tick().Phase);
    }

    [Fact]
    public void Shorter_work_setting_applies_immediately()
    {
        var s = Create(Fast with { WorkSeconds = 120 });
        Assert.Equal(Phase.Working, Run(s, 70).Phase);

        s.ApplySettings(Fast);

        Assert.Equal(Phase.Due, s.Tick().Phase);
    }
}
