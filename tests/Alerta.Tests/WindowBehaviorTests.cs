using System.Runtime.InteropServices;
using Alerta.Core;
using Alerta.Platform;
using Alerta.UI;

namespace Alerta.Tests;

public class WindowBehaviorTests
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    private static bool IsNoActivate(Form form) => (GetWindowLong(form.Handle, GWL_EXSTYLE) & WS_EX_NOACTIVATE) != 0;

    [Fact]
    public void Backdrop_never_takes_activation()
    {
        using var backdrop = new BackdropForm(Screen.PrimaryScreen!, 0.4);

        Assert.True(IsNoActivate(backdrop));
    }

    [Fact]
    public void Card_never_takes_activation_so_typing_cannot_press_its_buttons()
    {
        using var card = new CardForm("H", new Exercise("T", "B"), [new CardButton("X", () => { })], CardPlacement.Center, dismissible: false);

        Assert.True(IsNoActivate(card));
    }

    [Theory]
    [InlineData(3, false, true)]
    [InlineData(2, false, true)]
    [InlineData(4, false, true)]
    [InlineData(3, true, false)]
    [InlineData(5, false, false)]
    public void Fullscreen_counts_as_busy_unless_it_is_our_own_overlay(int state, bool foregroundIsOurs, bool expected) =>
        Assert.Equal(expected, BusyDetector.IsFullScreenBusy(state, foregroundIsOurs));
}
