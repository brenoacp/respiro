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
