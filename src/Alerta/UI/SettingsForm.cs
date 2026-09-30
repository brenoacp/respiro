using Alerta.Core;

namespace Alerta.UI;

internal sealed class SettingsForm : Form
{
    public Settings Result { get; private set; }

    public SettingsForm(Settings settings)
    {
        Result = settings;

        Text = "Respiro — Configurações";
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

        var tips = AddCheck(grid, "Mostrar dicas de saúde", settings.ShowHealthTips);
        var sound = AddCheck(grid, "Tocar som", settings.PlaySound);

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
            ShowHealthTips = tips.Checked,
            PlaySound = sound.Checked,
        }).Normalized();

        Controls.Add(grid);
    }

    private static CheckBox AddCheck(TableLayoutPanel grid, string label, bool value)
    {
        var box = new CheckBox { Text = label, Checked = value, AutoSize = true, Margin = new Padding(0, 6, 0, 6) };
        grid.Controls.Add(box);
        grid.SetColumnSpan(box, 2);
        return box;
    }

    private static NumericUpDown AddRow(TableLayoutPanel grid, string label, int value, int min, int max)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) });
        var input = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), Width = 80 };
        grid.Controls.Add(input);
        return input;
    }
}
