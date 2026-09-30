using Alerta.Core;

namespace Alerta.Tests;

public class SoundCueTests
{
    private static SchedulerSnapshot Snap(Phase phase, int stage = 0, int sequence = 1) =>
        new(phase, stage, sequence, 1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, false, null);

    [Fact]
    public void Plays_once_when_toast_appears()
    {
        var cue = new SoundCue();

        Assert.False(cue.ShouldPlay(Snap(Phase.Working), enabled: true));
        Assert.True(cue.ShouldPlay(Snap(Phase.Due, 1), enabled: true));
        Assert.False(cue.ShouldPlay(Snap(Phase.Due, 1), enabled: true));
    }

    [Fact]
    public void Plays_again_on_each_escalation_stage()
    {
        var cue = new SoundCue();
        cue.ShouldPlay(Snap(Phase.Due, 1), enabled: true);

        Assert.True(cue.ShouldPlay(Snap(Phase.Due, 2), enabled: true));
        Assert.True(cue.ShouldPlay(Snap(Phase.Due, 3), enabled: true));
    }

    [Fact]
    public void Plays_on_each_notify_only_repeat()
    {
        var cue = new SoundCue();
        cue.ShouldPlay(Snap(Phase.Due, 1, sequence: 1), enabled: true);

        Assert.True(cue.ShouldPlay(Snap(Phase.Due, 1, sequence: 2), enabled: true));
    }

    [Fact]
    public void Silent_during_break_and_when_leaving_alert()
    {
        var cue = new SoundCue();
        cue.ShouldPlay(Snap(Phase.Due, 1), enabled: true);

        Assert.False(cue.ShouldPlay(Snap(Phase.OnBreak), enabled: true));
        Assert.False(cue.ShouldPlay(Snap(Phase.Working), enabled: true));
    }

    [Fact]
    public void Silent_when_disabled()
    {
        var cue = new SoundCue();

        Assert.False(cue.ShouldPlay(Snap(Phase.Due, 1), enabled: false));
        Assert.False(cue.ShouldPlay(Snap(Phase.Due, 2), enabled: false));
    }
}
