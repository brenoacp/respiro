using Alerta.Platform;
using Microsoft.Win32;

namespace Alerta.Tests;

public sealed class PlatformTests : IDisposable
{
    private readonly string _root = $@"Software\AlertaTests\{Guid.NewGuid():N}";

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);

    private void WriteMicUsage(string subPath, long start, long stop)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"{_root}\{subPath}");
        key.SetValue("LastUsedTimeStart", start, RegistryValueKind.QWord);
        key.SetValue("LastUsedTimeStop", stop, RegistryValueKind.QWord);
    }

    [Fact]
    public void Non_packaged_app_with_open_microphone_is_busy()
    {
        WriteMicUsage(@"NonPackaged\C:#Program Files#Zoom#Zoom.exe", start: 133000000000000000, stop: 0);

        Assert.True(BusyDetector.IsMicrophoneInUse(_root));
    }

    [Fact]
    public void Packaged_app_with_open_microphone_is_busy()
    {
        WriteMicUsage("MSTeams_8wekyb3d8bbwe", start: 133000000000000000, stop: 0);

        Assert.True(BusyDetector.IsMicrophoneInUse(_root));
    }

    [Fact]
    public void Released_microphone_is_not_busy()
    {
        WriteMicUsage(@"NonPackaged\app.exe", start: 133000000000000000, stop: 133000000000000100);

        Assert.False(BusyDetector.IsMicrophoneInUse(_root));
    }

    [Fact]
    public void Missing_microphone_key_means_not_busy() =>
        Assert.False(BusyDetector.IsMicrophoneInUse($@"{_root}\does-not-exist"));

    [Fact]
    public void Startup_enable_then_disable()
    {
        var startup = new StartupRegistration($@"{_root}\Run");

        Assert.False(startup.IsEnabled());

        startup.Enable(@"C:\Apps\Respiro.exe");
        Assert.True(startup.IsEnabled());
        using (var key = Registry.CurrentUser.OpenSubKey($@"{_root}\Run"))
            Assert.Equal("\"C:\\Apps\\Respiro.exe\"", key!.GetValue("Respiro"));

        startup.Disable();
        Assert.False(startup.IsEnabled());
    }

    [Fact]
    public void Startup_disable_when_never_enabled_does_not_throw() =>
        new StartupRegistration($@"{_root}\Run").Disable();

    [Fact]
    public void Legacy_autostart_entry_is_replaced_by_the_new_one()
    {
        var legacy = new StartupRegistration($@"{_root}\Run", "Alerta");
        legacy.Enable(@"C:\Apps\Alerta.exe");
        var current = new StartupRegistration($@"{_root}\Run", "Respiro");

        current.MigrateFrom("Alerta", @"C:\Apps\Respiro.exe");

        Assert.False(legacy.IsEnabled());
        Assert.True(current.IsEnabled());
    }

    [Fact]
    public void Autostart_migration_without_legacy_entry_changes_nothing()
    {
        var current = new StartupRegistration($@"{_root}\Run", "Respiro");

        current.MigrateFrom("Alerta", @"C:\Apps\Respiro.exe");

        Assert.False(current.IsEnabled());
    }

    [Fact]
    public void Idle_monitor_reports_a_plausible_value() =>
        Assert.InRange(new IdleMonitor().IdleTime, TimeSpan.Zero, TimeSpan.FromDays(50));
}
