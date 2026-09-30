namespace Alerta.Core;

public enum Phase { Working, Due, OnBreak, Suspended }

/// <param name="Stage">0 unless <see cref="Phase.Due"/>; then 1 (toast), 2 (dim), 3 (lock).</param>
/// <param name="NotifySequence">Increments whenever a (re)notification should be shown.</param>
/// <param name="Progress">Fraction of the work interval done, clamped to 0..1.</param>
/// <param name="SuspendedUntil">UTC deadline while <see cref="Phase.Suspended"/>.</param>
public sealed record SchedulerSnapshot(
    Phase Phase,
    int Stage,
    int NotifySequence,
    double Progress,
    TimeSpan UntilBreak,
    TimeSpan BreakRemaining,
    TimeSpan LockRemaining,
    bool DeferredByBusy,
    DateTime? SuspendedUntil);
