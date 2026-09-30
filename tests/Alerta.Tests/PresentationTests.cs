using System.Drawing;
using Alerta.Core;

namespace Alerta.Tests;

public class PresentationTests
{
    private static SchedulerSnapshot Snap(
        Phase phase = Phase.Working,
        int stage = 0,
        TimeSpan? untilBreak = null,
        TimeSpan? breakRemaining = null,
        bool deferred = false,
        DateTime? suspendedUntil = null) =>
        new(phase, stage, 0, 0.5, untilBreak ?? TimeSpan.FromMinutes(10), breakRemaining ?? TimeSpan.Zero,
            TimeSpan.Zero, deferred, suspendedUntil);

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void Color_is_green_for_first_half(double progress) =>
        Assert.Equal(ColorRamp.Green.ToArgb(), ColorRamp.ForProgress(progress).ToArgb());

    [Fact]
    public void Color_is_amber_at_85_percent() =>
        Assert.Equal(ColorRamp.Amber.ToArgb(), ColorRamp.ForProgress(0.85).ToArgb());

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void Color_is_red_when_done(double progress) =>
        Assert.Equal(ColorRamp.Red.ToArgb(), ColorRamp.ForProgress(progress).ToArgb());

    [Fact]
    public void Color_between_green_and_amber_is_interpolated()
    {
        var mid = ColorRamp.ForProgress(0.675);

        Assert.InRange(mid.R, ColorRamp.Green.R, ColorRamp.Amber.R);
        Assert.NotEqual(ColorRamp.Green.ToArgb(), mid.ToArgb());
        Assert.NotEqual(ColorRamp.Amber.ToArgb(), mid.ToArgb());
    }

    [Fact]
    public void Status_rounds_minutes_up() =>
        Assert.Equal("Pausa em 13 min", StatusText.For(Snap(untilBreak: TimeSpan.FromSeconds(12 * 60 + 30))));

    [Fact]
    public void Status_under_a_minute() =>
        Assert.Equal("Pausa em menos de 1 min", StatusText.For(Snap(untilBreak: TimeSpan.FromSeconds(40))));

    [Fact]
    public void Status_due() =>
        Assert.Equal("Hora da pausa!", StatusText.For(Snap(Phase.Due, stage: 1)));

    [Fact]
    public void Status_on_break_shows_countdown() =>
        Assert.Equal("Em pausa — 3:05", StatusText.For(Snap(Phase.OnBreak, breakRemaining: TimeSpan.FromSeconds(185))));

    [Fact]
    public void Status_deferred_by_meeting() =>
        Assert.Equal("Pausa adiada: reunião ou tela cheia", StatusText.For(Snap(deferred: true)));

    [Fact]
    public void Status_suspended_shows_local_time()
    {
        var until = new DateTime(2026, 9, 30, 17, 30, 0, DateTimeKind.Utc);

        Assert.Equal($"Silenciado até {until.ToLocalTime():HH:mm}", StatusText.For(Snap(Phase.Suspended, suspendedUntil: until)));
    }

    [Theory]
    [InlineData(Phase.Working, 0, AlertView.None)]
    [InlineData(Phase.Suspended, 0, AlertView.None)]
    [InlineData(Phase.Due, 1, AlertView.Toast)]
    [InlineData(Phase.Due, 2, AlertView.Dim)]
    [InlineData(Phase.Due, 3, AlertView.Lock)]
    [InlineData(Phase.OnBreak, 0, AlertView.Break)]
    public void View_follows_phase_and_stage(Phase phase, int stage, AlertView expected) =>
        Assert.Equal(expected, AlertViews.For(Snap(phase, stage)));
}
