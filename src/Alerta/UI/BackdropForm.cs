namespace Alerta.UI;

/// <summary>Full-screen black layer over one monitor; opacity 0.4 dims, 0.92 locks.</summary>
internal sealed class BackdropForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private readonly Screen _screen;
    private bool _allowClose;

    public BackdropForm(Screen screen, double opacity)
    {
        _screen = screen;
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
            // No activation: a click on the backdrop must not make our window the "full-screen app".
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    // With per-monitor DPI, WinForms rescales the window when it lands on a monitor with another DPI.
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Bounds = _screen.Bounds;
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Bounds = _screen.Bounds;
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
