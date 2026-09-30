using System.Runtime.InteropServices;
using System.Security;
using Alerta.Core;
using Microsoft.Win32;

namespace Alerta.Platform;

/// <summary>Busy = full-screen app, presentation mode, or any app currently holding the microphone.</summary>
internal sealed class BusyDetector : IBusySource
{
    internal const string MicrophoneKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    private const int QUNS_BUSY = 2;
    private const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    private const int QUNS_PRESENTATION_MODE = 4;

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    private readonly string _micKeyPath;

    public BusyDetector(string micKeyPath = MicrophoneKeyPath) => _micKeyPath = micKeyPath;

    public bool IsBusy() => IsFullScreenOrPresenting() || IsMicrophoneInUse(_micKeyPath);

    private static bool IsFullScreenOrPresenting()
    {
        try
        {
            return SHQueryUserNotificationState(out var state) == 0
                && (state is QUNS_BUSY or QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Windows records mic usage per app under <paramref name="keyPath"/>\&lt;app&gt; (packaged) and
    /// <paramref name="keyPath"/>\NonPackaged\&lt;exe&gt;. LastUsedTimeStop == 0 means the app still holds it.
    /// </summary>
    internal static bool IsMicrophoneInUse(string keyPath)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(keyPath);
            return root is not null && AnyInUse(root, depth: 0);
        }
        catch (Exception e) when (e is SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool AnyInUse(RegistryKey key, int depth)
    {
        if (key.GetValue("LastUsedTimeStop") is long stop && stop == 0
            && key.GetValue("LastUsedTimeStart") is long start && start > 0)
            return true;

        if (depth >= 2) return false;

        foreach (var name in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(name);
            if (sub is not null && AnyInUse(sub, depth + 1)) return true;
        }
        return false;
    }
}
