namespace Alerta.Core;

public enum AlertView { None, Toast, Dim, Lock, Break }

public static class AlertViews
{
    public static AlertView For(SchedulerSnapshot s) => s.Phase switch
    {
        Phase.OnBreak => AlertView.Break,
        Phase.Due => s.Stage switch
        {
            3 => AlertView.Lock,
            2 => AlertView.Dim,
            _ => AlertView.Toast,
        },
        _ => AlertView.None,
    };
}
