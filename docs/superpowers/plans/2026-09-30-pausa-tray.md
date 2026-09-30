# Pausa (Alerta) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Windows tray app that shows a "breathing" icon next to the clock and escalates break reminders (toast → dimmed screen → 30 s lock), skipping reminders during meetings and treating real inactivity as a break.

**Architecture:** A pure, UI-free `BreakScheduler` state machine (`Core/`) is driven by a 1 s WinForms timer. It pulls time, idle time and "busy" state through injected interfaces and returns an immutable `SchedulerSnapshot`. Pure helpers turn snapshots into tray text, icon color and which alert window to show. `Platform/` wraps Win32/registry. `UI/` only renders snapshots and forwards button clicks back to the scheduler.

**Tech Stack:** C# 12, .NET 8 (`net8.0-windows`), WinForms, System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-30-pausa-tray-design.md`

**Deviations from spec file list (intentional, same behavior):**
- `ToastForm`, `DimOverlayForm`, `LockOverlayForm` and `BreakForm` are collapsed into two reusable windows:
  - `CardForm`, the exercise card. It is placed bottom-right (toast) or centered.
  - `BackdropForm`, the full-screen darkening, one per monitor.

  They are split this way because a WinForms `Opacity` applies to child controls, so the card must be a separate window above the backdrop.
- New pure helpers are added in `Core/`, so tray text, icon color and view selection are unit-testable:
  - `AlertViews`
  - `StatusText`
  - `ColorRamp`
- The icon re-renders whenever progress changes by 1 % (≈27 s on a 45 min cycle) instead of on a fixed 30 s timer.

## Global Constraints

- Target framework `net8.0-windows`, `UseWindowsForms` true, `Nullable` enable, `ImplicitUsings` enable. No NuGet packages in the app project.
- The UI language is pt-BR. Code, identifiers and commit messages are in English.
- Defaults (seconds):

  | Setting | Default |
  |---|---|
  | Work | 2700 |
  | Break | 300 |
  | IdleReset | 300 |
  | Stage2After | 120 |
  | Stage3After | 180 |
  | Lock | 30 |
  | NotifyRepeat | 300 |
  | BusyRecheck | 60 |
  | Mode | `Escalating` |

- Clamp ranges (seconds):

  | Setting | Range |
  |---|---|
  | Work | 60–14400 |
  | Break | 10–3600 |
  | IdleReset | 30–3600 |
  | Stage2After | 5–3600 |
  | Stage3After | 5–3600 |
  | Lock | 5–300 |
  | NotifyRepeat | 10–3600 |
  | BusyRecheck | 5–600 |

- The settings file lives at `%APPDATA%\Alerta\settings.json`. Enums are stored as strings.
- The scheduler works in UTC (`DateTime.UtcNow`). Only display code converts to local time.
- Stage-3 lock completion (30 s elapsed) counts as a forced micro-break and restarts the cycle.
- Single instance via mutex `Local\Alerta.SingleInstance`.
- Autostart value is `Alerta` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Its data is the quoted exe path.
- Every commit message ends with the session attribution trailer lines:
  - `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
  - `Claude-Session: https://claude.ai/code/session_01MxZzQwEKgUQxKtEMGQMYwv`

## Review Focus

1. A hand-edited settings file with 0, negative or huge numbers, or an unknown `Mode`, must load clamped or default values, never crash. Tested in Task 1: `Out_of_range_values_are_clamped`, `Unknown_mode_string_falls_back_to_defaults`.
2. "Adiar 15 min" from the menu early in a cycle must postpone the break, not shorten the remaining time. Tested in Task 2: `Snooze_while_working_pushes_break_back`, `Snooze_longer_than_worked_time_postpones_past_full_interval`.
3. Sitting in a long call without touching the keyboard must not count as rest. Tested in Task 2: `Idle_while_busy_does_not_count_as_break`.
4. The system clock moving backwards (NTP or DST) must not produce negative progress or crash. Tested in Task 2: `Clock_moving_backwards_is_ignored`.
5. A missing or unreadable microphone registry key must mean "not busy", not an exception. Tested in Task 5: `Missing_microphone_key_means_not_busy`.

---

### Task 1: Solution scaffold + Settings + SettingsStore

**Files:**
- Create: `Alerta.sln`, `.gitignore`
- Create: `src/Alerta/Alerta.csproj`, `src/Alerta/Program.cs` (placeholder, replaced in Task 7)
- Create: `src/Alerta/Core/Settings.cs`, `src/Alerta/Core/SettingsStore.cs`
- Create: `tests/Alerta.Tests/Alerta.Tests.csproj` (from template, edited)
- Test: `tests/Alerta.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `public enum AlertMode { Escalating, NotifyOnly }`
  - `public sealed record Settings`. It has int init properties `WorkSeconds`, `BreakSeconds`, `IdleResetSeconds`, `Stage2AfterSeconds`, `Stage3AfterSeconds`, `LockSeconds`, `NotifyRepeatSeconds`, `BusyRecheckSeconds` and `AlertMode Mode`.
  - `[JsonIgnore]` TimeSpan getters on `Settings`: `Work`, `Break`, `IdleReset`, `Stage2After`, `Stage3After`, `Lock`, `NotifyRepeat`, `BusyRecheck`.
  - `Settings Normalized()` and `static Settings Demo()`.
  - `public static class SettingsStore` with `Settings Load(string path)` and `bool Save(string path, Settings settings)`.

- [ ] **Step 1: Scaffold projects**

Run (Git Bash, from `C:\temp\alerta`):
```bash
dotnet new sln -n Alerta
dotnet new winforms -n Alerta -o src/Alerta -f net8.0
dotnet new xunit -n Alerta.Tests -o tests/Alerta.Tests -f net8.0
dotnet sln add src/Alerta/Alerta.csproj tests/Alerta.Tests/Alerta.Tests.csproj
dotnet add tests/Alerta.Tests reference src/Alerta
rm src/Alerta/Form1.cs src/Alerta/Form1.Designer.cs tests/Alerta.Tests/UnitTest1.cs
dotnet new gitignore
sed -i 's#<TargetFramework>net8.0</TargetFramework>#<TargetFramework>net8.0-windows</TargetFramework>\n    <UseWindowsForms>true</UseWindowsForms>#' tests/Alerta.Tests/Alerta.Tests.csproj
```

Overwrite `src/Alerta/Alerta.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
    <AssemblyName>Alerta</AssemblyName>
    <RootNamespace>Alerta</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Alerta.Tests" />
  </ItemGroup>

</Project>
```

Overwrite `src/Alerta/Program.cs` (placeholder so the project builds):
```csharp
namespace Alerta;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
    }
}
```

Run: `dotnet build`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 2: Write the failing tests**

`tests/Alerta.Tests/SettingsStoreTests.cs`:
```csharp
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
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~SettingsStoreTests`
Expected: build FAIL, `The type or namespace name 'Settings' could not be found`.

- [ ] **Step 4: Implement**

`src/Alerta/Core/Settings.cs`:
```csharp
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
```

`src/Alerta/Core/SettingsStore.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Alerta.Core;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Settings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Options);
                if (loaded is not null) return loaded.Normalized();
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // Fall through to defaults and rewrite the file.
        }

        var defaults = new Settings();
        Save(path, defaults);
        return defaults;
    }

    public static bool Save(string path, Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~SettingsStoreTests`
Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: scaffold solution with settings and JSON store"
```

---

### Task 2: BreakScheduler state machine

**Files:**
- Create: `src/Alerta/Core/Abstractions.cs`, `src/Alerta/Core/SchedulerSnapshot.cs`, `src/Alerta/Core/BreakScheduler.cs`
- Test: `tests/Alerta.Tests/Fakes.cs`, `tests/Alerta.Tests/BreakSchedulerTests.cs`

**Interfaces:**
- Consumes: `Settings` (with `Normalized()` and TimeSpan getters) and `AlertMode` from Task 1.
- Produces:
  - `public interface IClock { DateTime Now { get; } }` (UTC)
  - `public sealed class SystemClock : IClock` (returns `DateTime.UtcNow`)
  - `public interface IIdleSource { TimeSpan IdleTime { get; } }`
  - `public interface IBusySource { bool IsBusy(); }`
  - `public enum Phase { Working, Due, OnBreak, Suspended }`
  - `public sealed record SchedulerSnapshot(Phase Phase, int Stage, int NotifySequence, double Progress, TimeSpan UntilBreak, TimeSpan BreakRemaining, TimeSpan LockRemaining, bool DeferredByBusy, DateTime? SuspendedUntil)`
    - `Stage` is 0 unless the phase is `Due`, then 1–3.
    - `Progress` is between 0 and 1.
    - `SuspendedUntil` is in UTC.
  - `public sealed class BreakScheduler`
    - `ctor(Settings, IClock, IIdleSource, IBusySource)`
    - `SchedulerSnapshot Tick()`
    - `void ApplySettings(Settings)`
    - `void StartBreak()`
    - `void Snooze(TimeSpan)`
    - `void Skip()`
    - `void Suspend(DateTime untilUtc)`
    - `void Resume()`

**Rules the tests pin:**
- **Working**
  - The worked time grows by the real time elapsed between ticks.
  - A tick gap of `IdleReset` or more (sleep or hibernate) resets the cycle.
  - `IdleTime >= IdleReset` also resets the cycle, unless the machine is busy.
  - Reaching `Work` enters **Due**, unless busy. When busy it stays in Working with `DeferredByBusy`.
- **Due**
  - Stages come from the elapsed time: 1, then 2 at `Stage2After`, then 3 at `Stage2After + Stage3After`.
  - In `NotifyOnly` mode it always stays at stage 1, and `NotifySequence` goes up every `NotifyRepeat`.
  - Being busy at stage 1 or 2 drops it back to Working (deferred).
  - At stage 3, once `Lock` has elapsed, the cycle resets.
  - Idle time resets the cycle, the same as in Working.
- The busy source is polled only when needed: when due, or when idle is over the threshold. Polls are at most once per `BusyRecheck`.
- `Snooze(d)`
  - When Working, subtracts `d` from the worked time. The result can go negative, so the break is pushed back.
  - Otherwise, sets the worked time to `Work − d` and switches to Working.

- [ ] **Step 1: Write fakes and failing tests**

`tests/Alerta.Tests/Fakes.cs`:
```csharp
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
```

`tests/Alerta.Tests/BreakSchedulerTests.cs`:
```csharp
using Alerta.Core;

namespace Alerta.Tests;

public class BreakSchedulerTests
{
    private static readonly Settings Fast = new()
    {
        WorkSeconds = 60,
        BreakSeconds = 30,
        IdleResetSeconds = 45,
        Stage2AfterSeconds = 20,
        Stage3AfterSeconds = 20,
        LockSeconds = 10,
        NotifyRepeatSeconds = 30,
        BusyRecheckSeconds = 5,
    };

    private readonly FakeClock _clock = new();
    private readonly FakeIdle _idle = new();
    private readonly FakeBusy _busy = new();

    private BreakScheduler Create(Settings? settings = null) => new(settings ?? Fast, _clock, _idle, _busy);

    /// <summary>Advances the clock one second at a time, ticking after each step.</summary>
    private SchedulerSnapshot Run(BreakScheduler scheduler, int seconds)
    {
        var last = scheduler.Tick();
        for (var i = 0; i < seconds; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            last = scheduler.Tick();
        }
        return last;
    }

    [Fact]
    public void Starts_working_with_full_interval_remaining()
    {
        var snap = Create().Tick();

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Stage);
        Assert.Equal(0, snap.Progress);
        Assert.Equal(TimeSpan.FromSeconds(60), snap.UntilBreak);
    }

    [Fact]
    public void Becomes_due_when_work_interval_elapses()
    {
        var s = Create();

        Assert.Equal(Phase.Working, Run(s, 59).Phase);
        var snap = Run(s, 1);

        Assert.Equal(Phase.Due, snap.Phase);
        Assert.Equal(1, snap.Stage);
        Assert.Equal(1, snap.NotifySequence);
        Assert.Equal(1.0, snap.Progress);
        Assert.Equal(TimeSpan.Zero, snap.UntilBreak);
    }

    [Fact]
    public void Escalates_to_dim_then_lock_then_restarts_cycle()
    {
        var s = Create();
        Run(s, 60);

        Assert.Equal(1, Run(s, 19).Stage);
        Assert.Equal(2, Run(s, 1).Stage);

        var locked = Run(s, 20);
        Assert.Equal(3, locked.Stage);
        Assert.Equal(TimeSpan.FromSeconds(10), locked.LockRemaining);

        var after = Run(s, 10);
        Assert.Equal(Phase.Working, after.Phase);
        Assert.Equal(0, after.Progress);
    }

    [Fact]
    public void Notify_only_mode_never_escalates_and_repeats_notification()
    {
        var s = Create(Fast with { Mode = AlertMode.NotifyOnly });
        Run(s, 60);

        var snap = Run(s, 29);
        Assert.Equal(1, snap.Stage);
        Assert.Equal(1, snap.NotifySequence);

        Assert.Equal(2, Run(s, 1).NotifySequence);

        snap = Run(s, 30);
        Assert.Equal(3, snap.NotifySequence);
        Assert.Equal(1, snap.Stage);
        Assert.Equal(Phase.Due, snap.Phase);
    }

    [Fact]
    public void Idle_beyond_threshold_counts_as_break()
    {
        var s = Create();
        Run(s, 30);

        _idle.IdleTime = TimeSpan.FromSeconds(44);
        Assert.True(Run(s, 1).Progress > 0.5);

        _idle.IdleTime = TimeSpan.FromSeconds(45);
        var snap = Run(s, 1);
        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Idle_during_due_counts_as_break()
    {
        var s = Create();
        Run(s, 60);
        _idle.IdleTime = TimeSpan.FromSeconds(45);

        var snap = Run(s, 1);

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Idle_while_busy_does_not_count_as_break()
    {
        _busy.Busy = true;
        var s = Create();
        Run(s, 10);
        _idle.IdleTime = TimeSpan.FromSeconds(100);

        var snap = Run(s, 10);

        Assert.True(snap.Progress > 0.3);
    }

    [Fact]
    public void Clock_jump_longer_than_idle_threshold_counts_as_break()
    {
        var s = Create();
        Run(s, 30);
        _clock.Advance(TimeSpan.FromMinutes(10));

        var snap = s.Tick();

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Clock_moving_backwards_is_ignored()
    {
        var s = Create();
        Run(s, 30);
        _clock.Advance(TimeSpan.FromSeconds(-20));

        var snap = s.Tick();

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0.5, snap.Progress, 3);
    }

    [Fact]
    public void Busy_defers_due_until_busy_ends()
    {
        _busy.Busy = true;
        var s = Create();

        var snap = Run(s, 60);
        Assert.Equal(Phase.Working, snap.Phase);
        Assert.True(snap.DeferredByBusy);

        _busy.Busy = false;
        snap = Run(s, 5);
        Assert.Equal(Phase.Due, snap.Phase);
        Assert.False(snap.DeferredByBusy);
    }

    [Fact]
    public void Meeting_starting_during_toast_pulls_alert_back()
    {
        var s = Create();
        Run(s, 60);
        _busy.Busy = true;

        var snap = Run(s, 5);

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.True(snap.DeferredByBusy);
    }

    [Fact]
    public void Busy_source_is_not_polled_while_simply_working()
    {
        var s = Create();

        Run(s, 59);

        Assert.Equal(0, _busy.Calls);
    }

    [Fact]
    public void Start_break_counts_down_then_restarts_cycle()
    {
        var s = Create();
        Run(s, 60);
        s.StartBreak();

        var snap = s.Tick();
        Assert.Equal(Phase.OnBreak, snap.Phase);
        Assert.Equal(TimeSpan.FromSeconds(30), snap.BreakRemaining);

        snap = Run(s, 30);
        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Snooze_while_due_sets_remaining_to_snooze_length()
    {
        var s = Create();
        Run(s, 60);
        s.Snooze(TimeSpan.FromSeconds(15));

        var snap = s.Tick();
        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(TimeSpan.FromSeconds(15), snap.UntilBreak);

        Assert.Equal(Phase.Due, Run(s, 15).Phase);
    }

    [Fact]
    public void Snooze_while_working_pushes_break_back()
    {
        var s = Create();
        Run(s, 40);
        s.Snooze(TimeSpan.FromSeconds(15));

        Assert.Equal(TimeSpan.FromSeconds(35), s.Tick().UntilBreak);
    }

    [Fact]
    public void Snooze_longer_than_worked_time_postpones_past_full_interval()
    {
        var s = Create();
        Run(s, 10);
        s.Snooze(TimeSpan.FromSeconds(30));

        var snap = s.Tick();

        Assert.Equal(TimeSpan.FromSeconds(80), snap.UntilBreak);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Skip_restarts_cycle()
    {
        var s = Create();
        Run(s, 60);
        s.Skip();

        var snap = s.Tick();

        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
    }

    [Fact]
    public void Suspend_ignores_time_until_deadline()
    {
        var s = Create();
        var until = _clock.Now.AddSeconds(100);
        s.Suspend(until);

        var snap = Run(s, 99);
        Assert.Equal(Phase.Suspended, snap.Phase);
        Assert.Equal(until, snap.SuspendedUntil);

        snap = Run(s, 1);
        Assert.Equal(Phase.Working, snap.Phase);
        Assert.Equal(0, snap.Progress);
        Assert.Null(snap.SuspendedUntil);
    }

    [Fact]
    public void Resume_ends_suspension()
    {
        var s = Create();
        s.Suspend(_clock.Now.AddHours(1));
        s.Resume();

        Assert.Equal(Phase.Working, s.Tick().Phase);
    }

    [Fact]
    public void Shorter_work_setting_applies_immediately()
    {
        var s = Create(Fast with { WorkSeconds = 120 });
        Assert.Equal(Phase.Working, Run(s, 70).Phase);

        s.ApplySettings(Fast);

        Assert.Equal(Phase.Due, s.Tick().Phase);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~BreakSchedulerTests`
Expected: build FAIL, `The type or namespace name 'BreakScheduler' could not be found`.

- [ ] **Step 3: Implement**

`src/Alerta/Core/Abstractions.cs`:
```csharp
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
```

`src/Alerta/Core/SchedulerSnapshot.cs`:
```csharp
namespace Alerta.Core;

public enum Phase { Working, Due, OnBreak, Suspended }

/// <param name="Stage">0 unless <see cref="Phase.Due"/>; then 1 (toast), 2 (dim), 3 (lock).</param>
/// <param name="NotifySequence">Increments whenever a (re)notification should be shown.</param>
/// <param name="Progress">Fraction of the work interval done, clamped to 0..1.</param>
/// <param name="SuspendedUntil">UTC deadline while <see cref="Phase.Suspended"/>.</param>
public sealed record SchedulerSnapshot(
    Phase Phase,
    int Stage,
    int NotifySequence,
    double Progress,
    TimeSpan UntilBreak,
    TimeSpan BreakRemaining,
    TimeSpan LockRemaining,
    bool DeferredByBusy,
    DateTime? SuspendedUntil);
```

`src/Alerta/Core/BreakScheduler.cs`:
```csharp
namespace Alerta.Core;

public sealed class BreakScheduler
{
    private readonly IClock _clock;
    private readonly IIdleSource _idle;
    private readonly IBusySource _busy;
    private Settings _settings;

    private Phase _phase = Phase.Working;
    private TimeSpan _worked = TimeSpan.Zero;
    private DateTime _lastTick;
    private DateTime _dueSince;
    private DateTime _nextNotifyAt;
    private DateTime _breakEndsAt;
    private DateTime _suspendedUntil;
    private DateTime _nextBusyCheck = DateTime.MinValue;
    private bool _lastBusy;
    private int _notifySequence;

    public BreakScheduler(Settings settings, IClock clock, IIdleSource idle, IBusySource busy)
    {
        _settings = settings.Normalized();
        _clock = clock;
        _idle = idle;
        _busy = busy;
        _lastTick = clock.Now;
    }

    public void ApplySettings(Settings settings) => _settings = settings.Normalized();

    public SchedulerSnapshot Tick()
    {
        var now = _clock.Now;
        var delta = now - _lastTick;
        if (delta < TimeSpan.Zero) delta = TimeSpan.Zero;
        _lastTick = now;

        switch (_phase)
        {
            case Phase.Suspended:
                if (now >= _suspendedUntil) ResetCycle();
                break;

            case Phase.OnBreak:
                if (now >= _breakEndsAt) ResetCycle();
                break;

            case Phase.Working:
                if (RestedSinceLastTick(now, delta))
                {
                    ResetCycle();
                    break;
                }
                _worked += delta;
                if (_worked >= _settings.Work && !IsBusy(now)) EnterDue(now);
                break;

            case Phase.Due:
                if (RestedSinceLastTick(now, delta))
                {
                    ResetCycle();
                    break;
                }
                if (StageAt(now) == 3)
                {
                    if (now >= LockEndsAt) ResetCycle();
                    break;
                }
                if (IsBusy(now))
                {
                    _phase = Phase.Working;
                    break;
                }
                if (_settings.Mode == AlertMode.NotifyOnly && now >= _nextNotifyAt)
                {
                    _notifySequence++;
                    _nextNotifyAt = now + _settings.NotifyRepeat;
                }
                break;
        }

        return Snapshot(now);
    }

    public void StartBreak()
    {
        _phase = Phase.OnBreak;
        _breakEndsAt = _clock.Now + _settings.Break;
    }

    /// <summary>Working: pushes the break back by <paramref name="by"/>. Otherwise: next break in <paramref name="by"/>.</summary>
    public void Snooze(TimeSpan by)
    {
        _worked = _phase == Phase.Working ? _worked - by : _settings.Work - by;
        _phase = Phase.Working;
    }

    public void Skip() => ResetCycle();

    public void Suspend(DateTime untilUtc)
    {
        _phase = Phase.Suspended;
        _suspendedUntil = untilUtc;
    }

    public void Resume() => ResetCycle();

    private DateTime LockEndsAt => _dueSince + _settings.Stage2After + _settings.Stage3After + _settings.Lock;

    /// <summary>A long tick gap (sleep) or real inactivity outside a meeting counts as a break.</summary>
    private bool RestedSinceLastTick(DateTime now, TimeSpan delta) =>
        delta >= _settings.IdleReset || (_idle.IdleTime >= _settings.IdleReset && !IsBusy(now));

    private bool IsBusy(DateTime now)
    {
        if (now >= _nextBusyCheck)
        {
            _lastBusy = _busy.IsBusy();
            _nextBusyCheck = now + _settings.BusyRecheck;
        }
        return _lastBusy;
    }

    private void EnterDue(DateTime now)
    {
        _phase = Phase.Due;
        _dueSince = now;
        _notifySequence++;
        _nextNotifyAt = now + _settings.NotifyRepeat;
    }

    private void ResetCycle()
    {
        _phase = Phase.Working;
        _worked = TimeSpan.Zero;
        _lastTick = _clock.Now;
    }

    private int StageAt(DateTime now)
    {
        if (_settings.Mode == AlertMode.NotifyOnly) return 1;
        var elapsed = now - _dueSince;
        if (elapsed >= _settings.Stage2After + _settings.Stage3After) return 3;
        if (elapsed >= _settings.Stage2After) return 2;
        return 1;
    }

    private SchedulerSnapshot Snapshot(DateTime now)
    {
        var work = _settings.Work;
        var progress = Math.Clamp(_worked / work, 0.0, 1.0);
        var untilBreak = Max0(work - _worked);
        var stage = _phase == Phase.Due ? StageAt(now) : 0;

        return new SchedulerSnapshot(
            Phase: _phase,
            Stage: stage,
            NotifySequence: _notifySequence,
            Progress: progress,
            UntilBreak: untilBreak,
            BreakRemaining: _phase == Phase.OnBreak ? Max0(_breakEndsAt - now) : TimeSpan.Zero,
            LockRemaining: stage == 3 ? Max0(LockEndsAt - now) : TimeSpan.Zero,
            DeferredByBusy: _phase == Phase.Working && _worked >= work,
            SuspendedUntil: _phase == Phase.Suspended ? _suspendedUntil : null);
    }

    private static TimeSpan Max0(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~BreakSchedulerTests`
Expected: `Passed: 20, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add break scheduler state machine"
```

---

### Task 3: ExerciseCatalog

**Files:**
- Create: `src/Alerta/Core/ExerciseCatalog.cs`
- Test: `tests/Alerta.Tests/ExerciseCatalogTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record Exercise(string Title, string Instructions)`
  - `public sealed class ExerciseCatalog`
    - `ctor(IReadOnlyList<Exercise>? items = null, Random? random = null)` (throws `ArgumentException` on an empty list)
    - `Exercise Next()`
    - `static IReadOnlyList<Exercise> Default`

- [ ] **Step 1: Write the failing tests**

`tests/Alerta.Tests/ExerciseCatalogTests.cs`:
```csharp
using Alerta.Core;

namespace Alerta.Tests;

public class ExerciseCatalogTests
{
    [Fact]
    public void Default_catalog_has_at_least_eight_complete_exercises()
    {
        Assert.True(ExerciseCatalog.Default.Count >= 8);
        Assert.All(ExerciseCatalog.Default, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Title));
            Assert.False(string.IsNullOrWhiteSpace(e.Instructions));
        });
    }

    [Fact]
    public void Next_never_repeats_the_previous_exercise()
    {
        var catalog = new ExerciseCatalog(random: new Random(42));
        var previous = catalog.Next();

        for (var i = 0; i < 1000; i++)
        {
            var current = catalog.Next();
            Assert.NotEqual(previous, current);
            previous = current;
        }
    }

    [Fact]
    public void Single_item_catalog_returns_that_item_repeatedly()
    {
        var only = new Exercise("A", "B");
        var catalog = new ExerciseCatalog([only]);

        Assert.Equal(only, catalog.Next());
        Assert.Equal(only, catalog.Next());
    }

    [Fact]
    public void Empty_catalog_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ExerciseCatalog([]));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~ExerciseCatalogTests`
Expected: build FAIL, `The type or namespace name 'ExerciseCatalog' could not be found`.

- [ ] **Step 3: Implement**

`src/Alerta/Core/ExerciseCatalog.cs`:
```csharp
namespace Alerta.Core;

public sealed record Exercise(string Title, string Instructions);

public sealed class ExerciseCatalog
{
    public static IReadOnlyList<Exercise> Default { get; } =
    [
        new("Regra 20-20-20", "Olhe para algo a pelo menos 6 metros de distância por 20 segundos. Pisque devagar algumas vezes."),
        new("Alongue o pescoço", "Incline a cabeça para a direita por 15 s, depois para a esquerda. Termine com o queixo no peito por 15 s."),
        new("Solte os ombros", "Gire os ombros para trás 10 vezes e para frente 10 vezes. Depois aperte as escápulas por 5 s."),
        new("Punhos e dedos", "Estique o braço com a palma para cima e puxe os dedos para baixo com a outra mão por 15 s. Troque de lado."),
        new("Levante e caminhe", "Fique de pé e dê uma volta de 2 minutos. Vale ir até a janela ou buscar um café."),
        new("Beba água", "Levante, encha o copo e beba devagar. Hidratação também ajuda a manter o foco."),
        new("Respiração 4-7-8", "Inspire pelo nariz em 4 s, segure por 7 s e solte pela boca em 8 s. Repita 4 vezes."),
        new("Alongue as costas", "De pé, entrelace os dedos acima da cabeça e estique o corpo para cima por 20 s. Depois incline para cada lado."),
    ];

    private readonly IReadOnlyList<Exercise> _items;
    private readonly Random _random;
    private int _last = -1;

    public ExerciseCatalog(IReadOnlyList<Exercise>? items = null, Random? random = null)
    {
        _items = items ?? Default;
        if (_items.Count == 0) throw new ArgumentException("Catalog needs at least one exercise.", nameof(items));
        _random = random ?? Random.Shared;
    }

    public Exercise Next()
    {
        if (_items.Count == 1) return _items[0];

        int index;
        do index = _random.Next(_items.Count);
        while (index == _last);

        _last = index;
        return _items[index];
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~ExerciseCatalogTests`
Expected: `Passed: 4, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add pt-BR exercise catalog with non-repeating rotation"
```

---

### Task 4: Presentation helpers (ColorRamp, StatusText, AlertViews)

**Files:**
- Create: `src/Alerta/Core/ColorRamp.cs`, `src/Alerta/Core/StatusText.cs`, `src/Alerta/Core/AlertViews.cs`
- Test: `tests/Alerta.Tests/PresentationTests.cs`

**Interfaces:**
- Consumes: `SchedulerSnapshot`, `Phase` from Task 2.
- Produces:
  - `public static class ColorRamp` with the colors `Green`, `Amber`, `Red` and `Color ForProgress(double progress)`
  - `public static class StatusText` with `string For(SchedulerSnapshot s)`
  - `public enum AlertView { None, Toast, Dim, Lock, Break }`
  - `public static class AlertViews` with `AlertView For(SchedulerSnapshot s)`

- [ ] **Step 1: Write the failing tests**

`tests/Alerta.Tests/PresentationTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~PresentationTests`
Expected: build FAIL, `The name 'ColorRamp' does not exist in the current context`.

- [ ] **Step 3: Implement**

`src/Alerta/Core/ColorRamp.cs`:
```csharp
using System.Drawing;

namespace Alerta.Core;

/// <summary>Tray icon color: green for the first half, then to amber at 85 %, then to red at 100 %.</summary>
public static class ColorRamp
{
    public static readonly Color Green = Color.FromArgb(0x2E, 0x9E, 0x4F);
    public static readonly Color Amber = Color.FromArgb(0xF2, 0xA9, 0x00);
    public static readonly Color Red = Color.FromArgb(0xD9, 0x30, 0x25);

    public static Color ForProgress(double progress)
    {
        var p = double.IsNaN(progress) ? 0 : Math.Clamp(progress, 0, 1);
        if (p <= 0.5) return Green;
        if (p <= 0.85) return Lerp(Green, Amber, (p - 0.5) / 0.35);
        return Lerp(Amber, Red, (p - 0.85) / 0.15);
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * t),
            (int)Math.Round(a.G + (b.G - a.G) * t),
            (int)Math.Round(a.B + (b.B - a.B) * t));
    }
}
```

`src/Alerta/Core/StatusText.cs`:
```csharp
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
```

`src/Alerta/Core/AlertViews.cs`:
```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~PresentationTests`
Expected: `Passed: 20, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add icon color ramp, tray status text and alert view mapping"
```

---

### Task 5: Platform adapters (idle, busy, autostart)

**Files:**
- Create: `src/Alerta/Platform/IdleMonitor.cs`, `src/Alerta/Platform/BusyDetector.cs`, `src/Alerta/Platform/StartupRegistration.cs`
- Test: `tests/Alerta.Tests/PlatformTests.cs`

**Interfaces:**
- Consumes: `IIdleSource`, `IBusySource` from Task 2.
- Produces:
  - `internal sealed class IdleMonitor : IIdleSource`
  - `internal sealed class BusyDetector : IBusySource`
    - `ctor(string micKeyPath = MicrophoneKeyPath)`
    - `internal static bool IsMicrophoneInUse(string keyPath)`
  - `internal sealed class StartupRegistration`
    - `ctor(string keyPath = RunKeyPath, string valueName = "Alerta")`
    - `bool IsEnabled()`
    - `void Enable(string exePath)`
    - `void Disable()`

The tests write only under `HKCU\Software\AlertaTests\<guid>` and delete it afterwards.

- [ ] **Step 1: Write the failing tests**

`tests/Alerta.Tests/PlatformTests.cs`:
```csharp
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

        startup.Enable(@"C:\Apps\Alerta.exe");
        Assert.True(startup.IsEnabled());
        using (var key = Registry.CurrentUser.OpenSubKey($@"{_root}\Run"))
            Assert.Equal("\"C:\\Apps\\Alerta.exe\"", key!.GetValue("Alerta"));

        startup.Disable();
        Assert.False(startup.IsEnabled());
    }

    [Fact]
    public void Startup_disable_when_never_enabled_does_not_throw() =>
        new StartupRegistration($@"{_root}\Run").Disable();

    [Fact]
    public void Idle_monitor_reports_a_plausible_value() =>
        Assert.InRange(new IdleMonitor().IdleTime, TimeSpan.Zero, TimeSpan.FromDays(50));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~PlatformTests`
Expected: build FAIL, `The type or namespace name 'Platform' does not exist in the namespace 'Alerta'`.

- [ ] **Step 3: Implement**

`src/Alerta/Platform/IdleMonitor.cs`:
```csharp
using System.Runtime.InteropServices;
using Alerta.Core;

namespace Alerta.Platform;

internal sealed class IdleMonitor : IIdleSource
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    public TimeSpan IdleTime
    {
        get
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
            // Both values are 32-bit tick counts; unsigned subtraction handles the 49.7-day wraparound.
            var idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
            return TimeSpan.FromMilliseconds(idleMs);
        }
    }
}
```

`src/Alerta/Platform/BusyDetector.cs`:
```csharp
using System.Runtime.InteropServices;
using System.Security;
using Alerta.Core;
using Microsoft.Win32;

namespace Alerta.Platform;

/// <summary>Busy = full-screen app, presentation mode, or any app currently holding the microphone.</summary>
internal sealed class BusyDetector : IBusySource
{
    internal const string MicrophoneKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    private const int QUNS_BUSY = 2;
    private const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    private const int QUNS_PRESENTATION_MODE = 4;

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    private readonly string _micKeyPath;

    public BusyDetector(string micKeyPath = MicrophoneKeyPath) => _micKeyPath = micKeyPath;

    public bool IsBusy() => IsFullScreenOrPresenting() || IsMicrophoneInUse(_micKeyPath);

    private static bool IsFullScreenOrPresenting()
    {
        try
        {
            return SHQueryUserNotificationState(out var state) == 0
                && (state is QUNS_BUSY or QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Windows records mic usage per app under <paramref name="keyPath"/>\&lt;app&gt; (packaged) and
    /// <paramref name="keyPath"/>\NonPackaged\&lt;exe&gt;. LastUsedTimeStop == 0 means the app still holds it.
    /// </summary>
    internal static bool IsMicrophoneInUse(string keyPath)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(keyPath);
            return root is not null && AnyInUse(root, depth: 0);
        }
        catch (Exception e) when (e is SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool AnyInUse(RegistryKey key, int depth)
    {
        if (key.GetValue("LastUsedTimeStop") is long stop && stop == 0
            && key.GetValue("LastUsedTimeStart") is long start && start > 0)
            return true;

        if (depth >= 2) return false;

        foreach (var name in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(name);
            if (sub is not null && AnyInUse(sub, depth + 1)) return true;
        }
        return false;
    }
}
```

`src/Alerta/Platform/StartupRegistration.cs`:
```csharp
using Microsoft.Win32;

namespace Alerta.Platform;

internal sealed class StartupRegistration
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _keyPath;
    private readonly string _valueName;

    public StartupRegistration(string keyPath = RunKeyPath, string valueName = "Alerta")
    {
        _keyPath = keyPath;
        _valueName = valueName;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue(_valueName) is string;
    }

    public void Enable(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
        key.SetValue(_valueName, $"\"{exePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~PlatformTests`
Expected: `Passed: 7, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add idle, meeting/fullscreen and autostart platform adapters"
```

---

### Task 6: Tray icon renderer + alert windows + presenter

**Files:**
- Create: `src/Alerta/UI/TrayIconRenderer.cs`, `src/Alerta/UI/CardForm.cs`, `src/Alerta/UI/BackdropForm.cs`, `src/Alerta/UI/AlertPresenter.cs`
- Test: `tests/Alerta.Tests/TrayIconRendererTests.cs`

**Interfaces:**
- Consumes:
  - From Task 2: `BreakScheduler` (`StartBreak`, `Snooze`, `Skip`) and `SchedulerSnapshot`
  - From Task 3: `ExerciseCatalog`, `Exercise`
  - From Task 4: `AlertViews.For`, `AlertView`, `StatusText.Clock`
- Produces:
  - `internal static class TrayIconRenderer` with `Icon Render(double progress, Color color, int size)`
  - `internal sealed class AlertPresenter : IDisposable`
    - `ctor(BreakScheduler scheduler, ExerciseCatalog catalog, Action refresh)`
    - `void Apply(SchedulerSnapshot s)`

Windows are verified manually in Task 7 (`--demo`). Only the renderer gets an automated test.

- [ ] **Step 1: Write the failing test**

`tests/Alerta.Tests/TrayIconRendererTests.cs`:
```csharp
using System.Drawing;
using Alerta.UI;

namespace Alerta.Tests;

public class TrayIconRendererTests
{
    [Theory]
    [InlineData(0.0, 16)]
    [InlineData(0.5, 24)]
    [InlineData(1.0, 32)]
    public void Renders_icon_of_requested_size(double progress, int size)
    {
        using var icon = TrayIconRenderer.Render(progress, Color.Red, size);

        Assert.Equal(size, icon.Width);
        Assert.Equal(size, icon.Height);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Alerta.Tests --filter FullyQualifiedName~TrayIconRendererTests`
Expected: build FAIL, `The type or namespace name 'UI' does not exist in the namespace 'Alerta'`.

- [ ] **Step 3: Implement renderer and windows**

`src/Alerta/UI/TrayIconRenderer.cs`:
```csharp
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Alerta.UI;

/// <summary>Draws the "breathing" ring: grey track, colored progress arc, colored center dot.</summary>
internal static class TrayIconRenderer
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Render(double progress, Color color, int size)
    {
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var stroke = size * 0.16f;
            var inset = size * 0.09f + stroke / 2;
            var ring = new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset);

            using var track = new Pen(Color.FromArgb(90, 128, 128, 128), stroke);
            g.DrawEllipse(track, ring);

            var sweep = (float)(360 * Math.Clamp(double.IsNaN(progress) ? 0 : progress, 0, 1));
            if (sweep > 0.5f)
            {
                using var arc = new Pen(color, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(arc, ring, -90, sweep);
            }

            var dot = size * 0.28f;
            using var fill = new SolidBrush(color);
            g.FillEllipse(fill, (size - dot) / 2, (size - dot) / 2, dot, dot);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
```

`src/Alerta/UI/CardForm.cs`:
```csharp
using Alerta.Core;

namespace Alerta.UI;

internal enum CardPlacement { BottomRight, Center }

internal sealed record CardButton(string Text, Action OnClick, bool Primary = false);

/// <summary>Borderless exercise card used for the toast, the dim/lock overlays and the break screen.</summary>
internal sealed class CardForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x80;

    private readonly CardPlacement _placement;
    private readonly Label _countdown;
    private bool _allowClose;

    public CardForm(string heading, Exercise exercise, IReadOnlyList<CardButton> buttons, CardPlacement placement, bool dismissible)
    {
        _placement = placement;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(18);
        Font = new Font("Segoe UI", 10f);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };

        var header = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        header.Controls.Add(new Label
        {
            Text = heading,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9f),
            ForeColor = Color.FromArgb(0x5F, 0x63, 0x68),
            Margin = new Padding(0, 0, 24, 0),
        });
        if (dismissible)
        {
            var close = new Label { Text = "✕", AutoSize = true, Cursor = Cursors.Hand, ForeColor = Color.Gray };
            close.Click += (_, _) => Hide();
            header.Controls.Add(close);
        }

        var title = new Label
        {
            Text = exercise.Title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 14f),
            Margin = new Padding(0, 6, 0, 4),
        };
        var body = new Label { Text = exercise.Instructions, AutoSize = true, MaximumSize = new Size(340, 0) };
        _countdown = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 22f),
            Margin = new Padding(0, 8, 0, 0),
            Visible = false,
        };

        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 14, 0, 0) };
        foreach (var b in buttons)
        {
            var button = new Button
            {
                Text = b.Text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(8, 2, 8, 2),
                Margin = new Padding(0, 0, 8, 0),
            };
            if (b.Primary)
            {
                button.BackColor = Color.FromArgb(0x1A, 0x73, 0xE8);
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderSize = 0;
            }
            button.Click += (_, _) => b.OnClick();
            row.Controls.Add(button);
        }

        layout.Controls.Add(header);
        layout.Controls.Add(title);
        layout.Controls.Add(body);
        layout.Controls.Add(_countdown);
        layout.Controls.Add(row);
        Controls.Add(layout);
    }

    protected override bool ShowWithoutActivation => _placement == CardPlacement.BottomRight;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW; // keep out of Alt+Tab
            return cp;
        }
    }

    public void SetCountdown(string? text)
    {
        var visible = text is not null;
        if (_countdown.Visible != visible)
        {
            _countdown.Visible = visible;
            Reposition();
        }
        if (text is not null && _countdown.Text != text) _countdown.Text = text;
    }

    /// <summary>Only the presenter closes cards; Alt+F4 is ignored so it never holds a disposed form.</summary>
    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Reposition();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.FromArgb(0xDA, 0xDC, 0xE0), ButtonBorderStyle.Solid);
    }

    private void Reposition()
    {
        var area = Screen.PrimaryScreen!.WorkingArea;
        Location = _placement == CardPlacement.BottomRight
            ? new Point(area.Right - Width - 12, area.Bottom - Height - 12)
            : new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
    }
}
```

`src/Alerta/UI/BackdropForm.cs`:
```csharp
namespace Alerta.UI;

/// <summary>Full-screen black layer over one monitor; opacity 0.4 dims, 0.92 locks.</summary>
internal sealed class BackdropForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x80;
    private bool _allowClose;

    public BackdropForm(Screen screen, double opacity)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds;
        BackColor = Color.Black;
        Opacity = opacity;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }
}
```

`src/Alerta/UI/AlertPresenter.cs`:
```csharp
using Alerta.Core;

namespace Alerta.UI;

/// <summary>Keeps the alert windows in sync with the scheduler snapshot.</summary>
internal sealed class AlertPresenter : IDisposable
{
    private readonly BreakScheduler _scheduler;
    private readonly ExerciseCatalog _catalog;
    private readonly Action _refresh;
    private readonly List<BackdropForm> _backdrops = [];
    private CardForm? _card;
    private AlertView _view = AlertView.None;
    private Exercise? _exercise;
    private int _notifySequence;

    public AlertPresenter(BreakScheduler scheduler, ExerciseCatalog catalog, Action refresh)
    {
        _scheduler = scheduler;
        _catalog = catalog;
        _refresh = refresh;
    }

    public void Apply(SchedulerSnapshot s)
    {
        var view = AlertViews.For(s);

        if (view != _view)
        {
            Teardown();
            _view = view;
            if (view == AlertView.None) _exercise = null;
            else Build(view);
            _notifySequence = s.NotifySequence;
        }
        else if (view == AlertView.Toast && s.NotifySequence != _notifySequence)
        {
            _notifySequence = s.NotifySequence;
            if (_card is { Visible: false }) _card.Show(); // re-show a dismissed toast
        }

        _card?.SetCountdown(view switch
        {
            AlertView.Lock => StatusText.Clock(s.LockRemaining),
            AlertView.Break => StatusText.Clock(s.BreakRemaining),
            _ => null,
        });
    }

    public void Dispose() => Teardown();

    private void Build(AlertView view)
    {
        var exercise = _exercise ??= _catalog.Next();

        switch (view)
        {
            case AlertView.Toast:
                _card = new CardForm("HORA DE DESCANSAR", exercise, DueButtons(), CardPlacement.BottomRight, dismissible: true);
                _card.Show();
                break;

            case AlertView.Dim:
                ShowBackdrops(0.4);
                _card = new CardForm("VOCÊ MERECE UMA PAUSA", exercise, DueButtons(), CardPlacement.Center, dismissible: false);
                _card.Show(_backdrops[0]);
                break;

            case AlertView.Lock:
                ShowBackdrops(0.92);
                _card = new CardForm("PAUSA OBRIGATÓRIA", exercise,
                    [new CardButton("Emergência: pular", Act(_scheduler.Skip))],
                    CardPlacement.Center, dismissible: false);
                _card.Show(_backdrops[0]);
                break;

            case AlertView.Break:
                _card = new CardForm("EM PAUSA", exercise,
                    [new CardButton("Voltar ao trabalho", Act(_scheduler.Skip))],
                    CardPlacement.Center, dismissible: false);
                _card.Show();
                break;
        }
    }

    private IReadOnlyList<CardButton> DueButtons() =>
    [
        new CardButton("Iniciar pausa", Act(_scheduler.StartBreak), Primary: true),
        new CardButton("Adiar 5 min", Act(() => _scheduler.Snooze(TimeSpan.FromMinutes(5)))),
        new CardButton("Pular", Act(_scheduler.Skip)),
    ];

    private Action Act(Action action) => () =>
    {
        action();
        _refresh();
    };

    private void ShowBackdrops(double opacity)
    {
        foreach (var screen in Screen.AllScreens.OrderByDescending(s => s.Primary))
        {
            var backdrop = new BackdropForm(screen, opacity);
            _backdrops.Add(backdrop);
            backdrop.Show();
        }
    }

    private void Teardown()
    {
        _card?.ForceClose();
        _card = null;
        foreach (var backdrop in _backdrops) backdrop.ForceClose();
        _backdrops.Clear();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Alerta.Tests`
Expected: all tests pass (`Failed: 0`).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add tray icon renderer, alert card, backdrop and presenter"
```

---

### Task 7: Tray controller, settings dialog, entry point, publish

**Files:**
- Create: `src/Alerta/UI/TrayController.cs`, `src/Alerta/UI/SettingsForm.cs`
- Modify: `src/Alerta/Program.cs` (replace the placeholder)
- Create: `README.md`

**Interfaces:**
- Consumes:
  - Everything above
  - From Task 1: `SettingsStore.Load` / `Save`, `Settings.Demo()`
  - From Task 2: `BreakScheduler`, `SystemClock`
  - From Task 3: `ExerciseCatalog`
  - From Task 4: `ColorRamp.ForProgress`, `StatusText.For`
  - From Task 5: `IdleMonitor`, `BusyDetector`, `StartupRegistration`
  - From Task 6: `TrayIconRenderer.Render`, `AlertPresenter`
- Produces:
  - `internal sealed class TrayController : ApplicationContext` with `ctor(string settingsPath, bool demo)`
  - `internal sealed class SettingsForm : Form` with `ctor(Settings)` and `Settings Result`

- [ ] **Step 1: Implement SettingsForm**

`src/Alerta/UI/SettingsForm.cs`:
```csharp
using Alerta.Core;

namespace Alerta.UI;

internal sealed class SettingsForm : Form
{
    public Settings Result { get; private set; }

    public SettingsForm(Settings settings)
    {
        Result = settings;

        Text = "Alerta — Configurações";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 10f);

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        var work = AddRow(grid, "Trabalho (min)", settings.WorkSeconds / 60, 1, 240);
        var brk = AddRow(grid, "Pausa (min)", settings.BreakSeconds / 60, 1, 60);
        var idle = AddRow(grid, "Parado conta como pausa após (min)", settings.IdleResetSeconds / 60, 1, 60);
        var stage2 = AddRow(grid, "Escurecer a tela após (min)", settings.Stage2AfterSeconds / 60, 1, 60);
        var stage3 = AddRow(grid, "Bloquear após mais (min)", settings.Stage3AfterSeconds / 60, 1, 60);

        var ok = new Button { Text = "Salvar", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        grid.Controls.Add(buttons);
        grid.SetColumnSpan(buttons, 2);

        AcceptButton = ok;
        CancelButton = cancel;
        ok.Click += (_, _) => Result = (settings with
        {
            WorkSeconds = (int)work.Value * 60,
            BreakSeconds = (int)brk.Value * 60,
            IdleResetSeconds = (int)idle.Value * 60,
            Stage2AfterSeconds = (int)stage2.Value * 60,
            Stage3AfterSeconds = (int)stage3.Value * 60,
        }).Normalized();

        Controls.Add(grid);
    }

    private static NumericUpDown AddRow(TableLayoutPanel grid, string label, int value, int min, int max)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) });
        var input = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), Width = 80 };
        grid.Controls.Add(input);
        return input;
    }
}
```

- [ ] **Step 2: Implement TrayController**

`src/Alerta/UI/TrayController.cs`:
```csharp
using Alerta.Core;
using Alerta.Platform;

namespace Alerta.UI;

internal sealed class TrayController : ApplicationContext
{
    private static readonly Color BreakBlue = Color.FromArgb(0x1A, 0x73, 0xE8);

    private readonly string _settingsPath;
    private readonly bool _demo;
    private readonly BreakScheduler _scheduler;
    private readonly AlertPresenter _presenter;
    private readonly StartupRegistration _startup = new();
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private Settings _settings;
    private ToolStripMenuItem _resumeItem = null!;
    private ToolStripMenuItem _modeEscalating = null!;
    private ToolStripMenuItem _modeNotify = null!;
    private ToolStripMenuItem _startupItem = null!;
    private Icon? _icon;
    private int _iconKey = -1;
    private bool _settingsOpen;

    public TrayController(string settingsPath, bool demo)
    {
        _settingsPath = settingsPath;
        _demo = demo;
        _settings = demo ? Settings.Demo() : SettingsStore.Load(settingsPath);
        _scheduler = new BreakScheduler(_settings, new SystemClock(), new IdleMonitor(), new BusyDetector());
        _presenter = new AlertPresenter(_scheduler, new ExerciseCatalog(), Refresh);

        _tray = new NotifyIcon { Text = "Alerta", ContextMenuStrip = BuildMenu(), Visible = true };
        _tray.DoubleClick += (_, _) => OpenSettings();

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    private void Refresh()
    {
        var snapshot = _scheduler.Tick();
        _presenter.Apply(snapshot);
        UpdateIcon(snapshot);
        var text = (_demo ? "[demo] " : "") + StatusText.For(snapshot);
        _tray.Text = text.Length > 127 ? text[..127] : text;
        _resumeItem.Visible = snapshot.Phase == Phase.Suspended;
    }

    private void UpdateIcon(SchedulerSnapshot s)
    {
        var key = ((int)s.Phase << 8) | (int)(s.Progress * 100);
        if (key == _iconKey) return;
        _iconKey = key;

        var (progress, color) = s.Phase switch
        {
            Phase.Suspended => (0.0, Color.Gray),
            Phase.OnBreak => (1.0, BreakBlue),
            _ => (s.Progress, ColorRamp.ForProgress(s.Progress)),
        };

        var previous = _icon;
        _icon = TrayIconRenderer.Render(progress, color, SystemInformation.SmallIconSize.Width);
        _tray.Icon = _icon;
        previous?.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("Pausar agora", null, (_, _) => Do(_scheduler.StartBreak));
        menu.Items.Add("Adiar 15 min", null, (_, _) => Do(() => _scheduler.Snooze(TimeSpan.FromMinutes(15))));

        var silence = new ToolStripMenuItem("Silenciar");
        silence.DropDownItems.Add("Por 1 hora", null, (_, _) => Do(() => _scheduler.Suspend(DateTime.UtcNow.AddHours(1))));
        silence.DropDownItems.Add("Até amanhã", null, (_, _) => Do(() => _scheduler.Suspend(DateTime.Today.AddDays(1).ToUniversalTime())));
        menu.Items.Add(silence);

        _resumeItem = new ToolStripMenuItem("Retomar lembretes", null, (_, _) => Do(_scheduler.Resume)) { Visible = false };
        menu.Items.Add(_resumeItem);
        menu.Items.Add(new ToolStripSeparator());

        var mode = new ToolStripMenuItem("Modo");
        _modeEscalating = new ToolStripMenuItem("Escalonado", null, (_, _) => SetMode(AlertMode.Escalating));
        _modeNotify = new ToolStripMenuItem("Só notificação", null, (_, _) => SetMode(AlertMode.NotifyOnly));
        mode.DropDownItems.Add(_modeEscalating);
        mode.DropDownItems.Add(_modeNotify);
        menu.Items.Add(mode);

        menu.Items.Add("Configurações...", null, (_, _) => OpenSettings());

        _startupItem = new ToolStripMenuItem("Iniciar com o Windows", null, (_, _) => ToggleStartup())
        {
            Checked = _startup.IsEnabled(),
        };
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitThread());

        UpdateModeChecks();
        return menu;
    }

    private void Do(Action action)
    {
        action();
        Refresh();
    }

    private void SetMode(AlertMode mode) => ApplySettings(_settings with { Mode = mode });

    private void ApplySettings(Settings settings)
    {
        _settings = settings.Normalized();
        _scheduler.ApplySettings(_settings);
        if (!_demo && !SettingsStore.Save(_settingsPath, _settings))
            _tray.ShowBalloonTip(3000, "Alerta", "Não foi possível salvar as configurações.", ToolTipIcon.Warning);
        UpdateModeChecks();
        Refresh();
    }

    private void UpdateModeChecks()
    {
        _modeEscalating.Checked = _settings.Mode == AlertMode.Escalating;
        _modeNotify.Checked = _settings.Mode == AlertMode.NotifyOnly;
    }

    private void OpenSettings()
    {
        if (_settingsOpen) return;
        _settingsOpen = true;
        try
        {
            using var form = new SettingsForm(_settings);
            if (form.ShowDialog() == DialogResult.OK) ApplySettings(form.Result);
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    private void ToggleStartup()
    {
        try
        {
            if (_startup.IsEnabled()) _startup.Disable();
            else _startup.Enable(Environment.ProcessPath!);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _tray.ShowBalloonTip(3000, "Alerta", "Não foi possível alterar a inicialização com o Windows.", ToolTipIcon.Warning);
        }
        _startupItem.Checked = _startup.IsEnabled();
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _presenter.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _icon?.Dispose();
        base.ExitThreadCore();
    }
}
```

- [ ] **Step 3: Replace Program.cs**

`src/Alerta/Program.cs`:
```csharp
using Alerta.UI;

namespace Alerta;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\Alerta.SingleInstance", out var createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();

        var demo = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Alerta", "settings.json");

        Application.Run(new TrayController(settingsPath, demo));
    }
}
```

- [ ] **Step 4: Build and run all tests**

Run: `dotnet build && dotnet test`
Expected: `Build succeeded`, then all tests pass with `Failed: 0`.

- [ ] **Step 5: Manual demo verification**

Run: `dotnet run --project src/Alerta -- --demo`

The demo uses work 60 s, stages every 20 s, lock 10 s, idle 45 s and break 30 s. Check each item and write down anything that fails:

1. A ring icon appears in the tray, near the clock. The tooltip reads `[demo] Pausa em 1 min`.
2. Keep moving the mouse. The ring fills, and its color goes from green to amber to red.
3. At about 60 s, a card appears bottom-right above the clock with an exercise and three buttons. It does not steal focus from the window you are typing in.
4. Ignore it for 20 s. All monitors dim (40 %) and the card is centered.
5. Ignore it for 20 s more. The screen goes almost black with a countdown from `0:10` and only `Emergência: pular`. Alt+F4 does not close it. After 10 s everything closes and the icon is green again.
6. Wait for the next toast and click `Iniciar pausa`. A centered card counts down from `0:30`. When it ends, a new cycle starts.
7. Wait for the next toast and click `✕`. The toast hides, but the escalation to dim still happens at +20 s.
8. Menu → `Modo` → `Só notificação`. When due, only the toast appears. If dismissed, it comes back every 20 s and never dims.
9. Leave the mouse and keyboard alone for 45 s. The icon resets to green, empty.
10. Open a YouTube video in full screen (F11) until work time is reached. No alert appears, and the tooltip shows `Pausa adiada: reunião ou tela cheia`. Exit full screen: the toast appears within about 5 s.
11. Start the Windows Sound Recorder app, or any app that uses the microphone. Same as item 10.
12. Menu → `Silenciar` → `Por 1 hora`. The icon turns gray, the tooltip reads `Silenciado até HH:mm`, and `Retomar lembretes` appears. Clicking it resumes.
13. Launch a second instance (`dotnet run --project src/Alerta -- --demo` again). It exits immediately, and there is only one tray icon.
14. Menu → `Sair`. The icon disappears and no windows are left.

Then run without `--demo`:
- `Configurações...` shows the defaults (45 / 5 / 5 / 2 / 3).
- Change the work time to 30 and save. `%APPDATA%\Alerta\settings.json` now shows `"WorkSeconds": 1800`.
- `Iniciar com o Windows` toggles `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Alerta`. Check with `reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v Alerta`.

- [ ] **Step 6: Publish single exe**

Run: `dotnet publish src/Alerta -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish`
Expected: `publish/Alerta.exe` exists. Double-clicking it starts the tray app. Add `publish/` to `.gitignore`.

- [ ] **Step 7: README**

`README.md`:
````markdown
# Alerta

App de bandeja do Windows que lembra você de fazer pausas.

- Ícone ao lado do relógio muda de verde → âmbar → vermelho conforme a pausa se aproxima.
- Modo **Escalonado**: aviso discreto → tela escurecida → bloqueio de 30 s (com "Emergência: pular").
- Modo **Só notificação**: apenas o aviso, repetido a cada 5 min.
- Ficar 5 min sem mexer no computador conta como pausa.
- Não interrompe em tela cheia, apresentação ou com o microfone em uso (Teams, Zoom, Meet).

## Rodar

```bash
dotnet run --project src/Alerta            # normal
dotnet run --project src/Alerta -- --demo  # intervalos curtos para teste
dotnet test
```

## Publicar

```bash
dotnet publish src/Alerta -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Configurações: `%APPDATA%\Alerta\settings.json`.
````

- [ ] **Step 8: Commit**

```bash
echo "publish/" >> .gitignore
git add -A
git commit -m "feat: add tray controller, settings dialog and entry point"
```
