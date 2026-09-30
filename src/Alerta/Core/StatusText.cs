namespace Alerta.Core;

/// <summary>Tray tooltip text (NotifyIcon.Text, max 127 chars).</summary>
public static class StatusText
{
    public static string For(SchedulerSnapshot s) => s.Phase switch
    {
        Phase.Suspended => $"Silenciado até {s.SuspendedUntil.GetValueOrDefault().ToLocalTime():HH:mm}",
        Phase.OnBreak => $"Em pausa — {Clock(s.BreakRemaining)}",
        Phase.Due => "Hora da pausa!",
        _ when s.DeferredByBusy => "Pausa adiada: reunião ou tela cheia",
        _ when s.UntilBreak < TimeSpan.FromMinutes(1) => "Pausa em menos de 1 min",
        _ => $"Pausa em {(int)Math.Ceiling(s.UntilBreak.TotalMinutes)} min",
    };

    public static string Clock(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
