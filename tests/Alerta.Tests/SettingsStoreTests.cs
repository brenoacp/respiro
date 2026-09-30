using Alerta.Core;

namespace Alerta.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "alerta-tests", Guid.NewGuid().ToString("N"));
    private string PathInDir => Path.Combine(_dir, "sub", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_file_returns_defaults_and_creates_file()
    {
        var settings = SettingsStore.Load(PathInDir);

        Assert.Equal(new Settings(), settings);
        Assert.True(File.Exists(PathInDir));
    }

    [Fact]
    public void Save_then_load_round_trips_and_stores_mode_as_string()
    {
        var original = new Settings { WorkSeconds = 1800, Mode = AlertMode.NotifyOnly };

        Assert.True(SettingsStore.Save(PathInDir, original));

        Assert.Contains("\"NotifyOnly\"", File.ReadAllText(PathInDir));
        Assert.Equal(original, SettingsStore.Load(PathInDir));
    }

    [Fact]
    public void Corrupt_file_returns_defaults_and_rewrites_file()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathInDir)!);
        File.WriteAllText(PathInDir, "{not json");

        Assert.Equal(new Settings(), SettingsStore.Load(PathInDir));
        Assert.Contains("WorkSeconds", File.ReadAllText(PathInDir));
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathInDir)!);
        File.WriteAllText(PathInDir, """{"WorkSeconds":0,"BreakSeconds":-5,"LockSeconds":99999,"Mode":7}""");

        var settings = SettingsStore.Load(PathInDir);

        Assert.Equal(60, settings.WorkSeconds);
        Assert.Equal(10, settings.BreakSeconds);
        Assert.Equal(300, settings.LockSeconds);
        Assert.Equal(AlertMode.Escalating, settings.Mode);
        Assert.Equal(300, settings.IdleResetSeconds);
    }

    [Fact]
    public void Unknown_mode_string_falls_back_to_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathInDir)!);
        File.WriteAllText(PathInDir, """{"WorkSeconds":1200,"Mode":"Foo"}""");

        Assert.Equal(new Settings(), SettingsStore.Load(PathInDir));
    }

    [Fact]
    public void Timespan_helpers_match_seconds()
    {
        var s = new Settings();

        Assert.Equal(TimeSpan.FromMinutes(45), s.Work);
        Assert.Equal(TimeSpan.FromMinutes(5), s.Break);
        Assert.Equal(TimeSpan.FromSeconds(30), s.Lock);
    }
}
