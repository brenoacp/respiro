using Alerta.Core;

namespace Alerta.UI;

/// <summary>Keeps the alert windows in sync with the scheduler snapshot.</summary>
internal sealed class AlertPresenter : IDisposable
{
    private readonly BreakScheduler _scheduler;
    private readonly Func<Exercise> _nextExercise;
    private readonly Action _refresh;
    private readonly List<BackdropForm> _backdrops = [];
    private CardForm? _card;
    private AlertView _view = AlertView.None;
    private Exercise? _exercise;
    private int _notifySequence;

    public AlertPresenter(BreakScheduler scheduler, Func<Exercise> nextExercise, Action refresh)
    {
        _scheduler = scheduler;
        _nextExercise = nextExercise;
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
        var exercise = _exercise ??= _nextExercise();

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
