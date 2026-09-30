using System.Text.Json.Serialization;

namespace Alerta.Core;

public enum AlertMode { Escalating, NotifyOnly }

public sealed record Settings
{
    public int WorkSeconds { get; init; } = 45 * 60;
    public int BreakSeconds { get; init; } = 5 * 60;
    public int IdleResetSeconds { get; init; } = 5 * 60;
    public int Stage2AfterSeconds { get; init; } = 2 * 60;
    public int Stage3AfterSeconds { get; init; } = 3 * 60;
    public int LockSeconds { get; init; } = 30;
    public int NotifyRepeatSeconds { get; init; } = 5 * 60;
    public int BusyRecheckSeconds { get; init; } = 60;
    public AlertMode Mode { get; init; } = AlertMode.Escalating;
    public bool ShowHealthTips { get; init; } = true;
    public bool PlaySound { get; init; } = true;

    [JsonIgnore] public TimeSpan Work => TimeSpan.FromSeconds(WorkSeconds);
    [JsonIgnore] public TimeSpan Break => TimeSpan.FromSeconds(BreakSeconds);
    [JsonIgnore] public TimeSpan IdleReset => TimeSpan.FromSeconds(IdleResetSeconds);
    [JsonIgnore] public TimeSpan Stage2After => TimeSpan.FromSeconds(Stage2AfterSeconds);
    [JsonIgnore] public TimeSpan Stage3After => TimeSpan.FromSeconds(Stage3AfterSeconds);
    [JsonIgnore] public TimeSpan Lock => TimeSpan.FromSeconds(LockSeconds);
    [JsonIgnore] public TimeSpan NotifyRepeat => TimeSpan.FromSeconds(NotifyRepeatSeconds);
    [JsonIgnore] public TimeSpan BusyRecheck => TimeSpan.FromSeconds(BusyRecheckSeconds);

    public Settings Normalized() => this with
    {
        WorkSeconds = Math.Clamp(WorkSeconds, 60, 14400),
        BreakSeconds = Math.Clamp(BreakSeconds, 10, 3600),
        IdleResetSeconds = Math.Clamp(IdleResetSeconds, 30, 3600),
        Stage2AfterSeconds = Math.Clamp(Stage2AfterSeconds, 5, 3600),
        Stage3AfterSeconds = Math.Clamp(Stage3AfterSeconds, 5, 3600),
        LockSeconds = Math.Clamp(LockSeconds, 5, 300),
        NotifyRepeatSeconds = Math.Clamp(NotifyRepeatSeconds, 10, 3600),
        BusyRecheckSeconds = Math.Clamp(BusyRecheckSeconds, 5, 600),
        Mode = Enum.IsDefined(Mode) ? Mode : AlertMode.Escalating,
    };

    /// <summary>Short intervals for manual testing (<c>--demo</c>). Never persisted.</summary>
    public static Settings Demo() => new()
    {
        WorkSeconds = 60,
        BreakSeconds = 30,
        IdleResetSeconds = 45,
        Stage2AfterSeconds = 20,
        Stage3AfterSeconds = 20,
        LockSeconds = 10,
        NotifyRepeatSeconds = 20,
        BusyRecheckSeconds = 5,
    };
}
