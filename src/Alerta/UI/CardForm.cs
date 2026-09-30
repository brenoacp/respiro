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
