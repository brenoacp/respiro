using Alerta.Core;

namespace Alerta.Tests;

internal sealed class FakeClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 9, 30, 9, 0, 0, DateTimeKind.Utc);
    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class FakeIdle : IIdleSource
{
    public TimeSpan IdleTime { get; set; }
}

internal sealed class FakeBusy : IBusySource
{
    public bool Busy { get; set; }
    public int Calls { get; private set; }

    public bool IsBusy()
    {
        Calls++;
        return Busy;
    }
}
