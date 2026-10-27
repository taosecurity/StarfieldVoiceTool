using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

using Mutagen.Bethesda;
using Mutagen.Bethesda.Installs;

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
        path = CleanUpTypedPath(path);
        if (path.Length == 0) return null;
        if (IsDataFolder(path)) return path;

        var dataSubfolder = Path.Combine(path, "Data");
        return IsDataFolder(dataSubfolder) ? dataSubfolder : null;
    }

    /// <summary>
    /// Tidy a path typed or pasted by the user: trim whitespace and surrounding quotes (as Windows'
    /// "Copy as path" adds), and expand a leading ~ to the home folder.
    /// </summary>
    public static string CleanUpTypedPath(string path)
    {
        path = path.Trim();
        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"') path = path[1..^1].Trim();

        if (path == "~" || path.StartsWith("~/"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = home + path[1..];
        }

        return path;
    }

    /// <summary>
    /// Look for an installed copy of Starfield (Steam, Xbox, and Steam/Proton on Linux), or return null.
    /// </summary>
    public string? TryAutoDetectGamePath()
    {
        try
        {
            if (GameLocations.TryGetDataFolder(GameRelease.Starfield, out var dataFolder))
            {
                return dataFolder.Path;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Looking up the Starfield install failed");
        }

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
