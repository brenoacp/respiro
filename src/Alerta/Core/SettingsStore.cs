using System.Text.Json;
using System.Text.Json.Serialization;

namespace Alerta.Core;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Settings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Options);
                if (loaded is not null) return loaded.Normalized();
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // Fall through to defaults and rewrite the file.
        }

        var defaults = new Settings();
        Save(path, defaults);
        return defaults;
    }

    /// <summary>Copies settings from an older app name's folder, unless the new file already exists.</summary>
    public static void MigrateLegacy(string legacyPath, string path)
    {
        try
        {
            if (File.Exists(path) || !File.Exists(legacyPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(legacyPath, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Start from defaults.
        }
    }

    public static bool Save(string path, Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
