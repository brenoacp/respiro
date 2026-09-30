using Microsoft.Win32;

namespace Alerta.Platform;

internal sealed class StartupRegistration
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _keyPath;
    private readonly string _valueName;

    public StartupRegistration(string keyPath = RunKeyPath, string valueName = "Respiro")
    {
        _keyPath = keyPath;
        _valueName = valueName;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue(_valueName) is string;
    }

    public void Enable(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
        key.SetValue(_valueName, $"\"{exePath}\"");
    }

    /// <summary>Moves an autostart entry left by an older app name to this entry.</summary>
    public void MigrateFrom(string legacyValueName, string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
        if (key?.GetValue(legacyValueName) is not string) return;
        key.DeleteValue(legacyValueName, throwOnMissingValue: false);
        key.SetValue(_valueName, $"\"{exePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }
}
