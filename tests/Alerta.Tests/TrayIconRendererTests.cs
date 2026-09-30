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
