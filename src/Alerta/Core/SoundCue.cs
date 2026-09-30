namespace Alerta.Core;

/// <summary>Decides when to beep: once per alert stage appearing, and once per notify-only repeat.</summary>
public sealed class SoundCue
{
    private AlertView _lastView = AlertView.None;
    private int _lastSequence;

    public bool ShouldPlay(SchedulerSnapshot s, bool enabled)
    {
        var view = AlertViews.For(s);
        var changed = view != _lastView || s.NotifySequence != _lastSequence;
        _lastView = view;
        _lastSequence = s.NotifySequence;

        var isAlert = view is AlertView.Toast or AlertView.Dim or AlertView.Lock;
        return enabled && isAlert && changed;
    }
}
