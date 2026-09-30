namespace Alerta.Core;

/// <summary>Current time in UTC.</summary>
public interface IClock
{
    DateTime Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.UtcNow;
}

/// <summary>Time since the last keyboard or mouse input.</summary>
public interface IIdleSource
{
    TimeSpan IdleTime { get; }
}

/// <summary>True while the user is in a call, presenting, or running something full screen.</summary>
public interface IBusySource
{
    bool IsBusy();
}
