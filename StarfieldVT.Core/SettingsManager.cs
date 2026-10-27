using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

using Serilog;

using StarfieldVT.Core.Filesystem;

namespace StarfieldVT.Core;

public class AppSettings
{
    public string? StarfieldDataPath { get; set; }
}

public class SettingsManager
{
    private static readonly string SettingsFilePath =
        Path.Combine(AppDataFolder.GetAppDataFolder(), "settings.json");

    private static readonly Lazy<SettingsManager> LazyInstance = new(() => new SettingsManager());
    public static SettingsManager Instance => LazyInstance.Value;

    private AppSettings _settings;

    private SettingsManager()
    {
        _settings = Load();
    }

    public string? StarfieldDataPath
    {
        get => _settings.StarfieldDataPath;
        set
        {
            _settings.StarfieldDataPath = value;
            Save();
        }
    }

    public bool HasValidGamePath()
    {
        return !string.IsNullOrEmpty(StarfieldDataPath) && IsDataFolder(StarfieldDataPath);
    }

    /// <summary>A Starfield Data folder is recognised by the Starfield.esm inside it.</summary>
    public static bool IsDataFolder(string path) => File.Exists(Path.Combine(path, "Starfield.esm"));

    /// <summary>
    /// Accept either the Data folder or the game's install folder, and return the Data folder,
    /// or null if neither contains Starfield.esm.
    /// </summary>
    public static string? NormalizeDataFolder(string path)
    {
        if (IsDataFolder(path)) return path;

        var dataSubfolder = Path.Combine(path, "Data");
        return IsDataFolder(dataSubfolder) ? dataSubfolder : null;
    }

    public string? TryAutoDetectGamePath()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        try
        {
            return StarfieldManager.GetGamePathFromRegistry();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Auto-detection of Starfield data path failed");
            return null;
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings from {Path}", SettingsFilePath);
        }

        return new AppSettings();
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to {Path}", SettingsFilePath);
        }
    }
}
