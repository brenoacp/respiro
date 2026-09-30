using Alerta.Core;
using Alerta.UI;

namespace Alerta;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\Respiro.SingleInstance", out var createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();

        var demo = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsPath = Path.Combine(appData, "Respiro", "settings.json");
        SettingsStore.MigrateLegacy(Path.Combine(appData, "Alerta", "settings.json"), settingsPath);

        Application.Run(new TrayController(settingsPath, demo));
    }
}
