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
