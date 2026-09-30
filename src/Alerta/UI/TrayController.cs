using System.Media;
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
    private readonly ExerciseCatalog _catalog = new();
    private readonly SoundCue _soundCue = new();
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private Settings _settings;
    private ToolStripMenuItem _resumeItem = null!;
    private ToolStripMenuItem _modeEscalating = null!;
    private ToolStripMenuItem _modeNotify = null!;
    private ToolStripMenuItem _startupItem = null!;
    private ToolStripMenuItem _tipsItem = null!;
    private ToolStripMenuItem _soundItem = null!;
    private Icon? _icon;
    private int _iconKey = -1;
    private bool _settingsOpen;

    public TrayController(string settingsPath, bool demo)
    {
        _settingsPath = settingsPath;
        _demo = demo;
        _settings = demo ? Settings.Demo() : SettingsStore.Load(settingsPath);
        _scheduler = new BreakScheduler(_settings, new SystemClock(), new IdleMonitor(), new BusyDetector());
        _presenter = new AlertPresenter(_scheduler, () => _catalog.NextFor(_settings), Refresh);

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
        if (_soundCue.ShouldPlay(snapshot, _settings.PlaySound)) SystemSounds.Asterisk.Play();
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

        _tipsItem = new ToolStripMenuItem("Mostrar dicas de saúde", null,
            (_, _) => ApplySettings(_settings with { ShowHealthTips = !_settings.ShowHealthTips }));
        _soundItem = new ToolStripMenuItem("Tocar som", null,
            (_, _) => ApplySettings(_settings with { PlaySound = !_settings.PlaySound }));
        menu.Items.Add(_tipsItem);
        menu.Items.Add(_soundItem);

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
        _tipsItem.Checked = _settings.ShowHealthTips;
        _soundItem.Checked = _settings.PlaySound;
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
