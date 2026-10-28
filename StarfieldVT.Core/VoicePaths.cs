namespace StarfieldVT.Core;

/// <summary>
/// Pure helpers for interpreting voice archive names and the paths of files inside them.
/// </summary>
public static class VoicePaths
{
    private static readonly string[] PluginExtensions = [".esm", ".esp", ".esl"];
    private const string VoicesMarker = " - voices";

    public static bool IsPluginName(string name) =>
        PluginExtensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public static string[] SplitArchivePath(string archivePath) =>
        archivePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// The plugin folder a voice file lives under, e.g. "starfield.esm" for
    /// "sound/voice/starfield.esm/npcmboe/00123456.wem".
    /// </summary>
    public static string? GetPluginFolder(string archivePath) =>
        SplitArchivePath(archivePath).FirstOrDefault(IsPluginName);

    /// <summary>The voice type folder, which is the directory directly containing the file.</summary>
    public static string? GetVoiceType(string archivePath)
    {
        var parts = SplitArchivePath(archivePath);
        return parts.Length >= 2 ? parts[^2] : null;
    }

    /// <summary>
    /// The DIAL response's WEM id carries the plugin's load order index in its top byte; inside the
    /// archive the file is named with that byte zeroed.
    /// </summary>
    public static string GetArchiveWemFileName(uint wemFile) => "00" + wemFile.ToString("x8")[2..] + ".wem";

    /// <summary>
    /// True for archives holding English (or language-neutral) voice files, e.g.
    /// "Starfield - Voices01", "Starfield - VoicesPatch", "SFBGS003 - Voices_en".
    /// False for other languages, e.g. "SFBGS003 - Voices_de" or "Mod - Voices_fr_ps".
    /// </summary>
    public static bool IsEnglishVoiceArchive(string archiveNameWithoutExtension)
    {
        var name = archiveNameWithoutExtension.ToLowerInvariant();
        var index = name.LastIndexOf(VoicesMarker, StringComparison.Ordinal);
        if (index < 0) return false;

        var suffix = name[(index + VoicesMarker.Length)..];
        if (suffix.Length == 0 || suffix == "patch" || suffix.All(char.IsDigit)) return true;

        return suffix.StartsWith("_en") && (suffix.Length == 3 || !char.IsLetter(suffix[3]));
    }

    /// <summary>
    /// True for archives localized to a language other than English, e.g. "Mod - Main_de" or
    /// "Mod - Voices_fr_ps". Only the part after " - " is checked, so a mod named "Make_it_so" is fine.
    /// </summary>
    public static bool IsOtherLanguageArchive(string archiveNameWithoutExtension)
    {
        var name = archiveNameWithoutExtension.ToLowerInvariant();
        var dash = name.LastIndexOf(" - ", StringComparison.Ordinal);
        var suffix = dash >= 0 ? name[(dash + 3)..] : name;
        return suffix.Split('_').Skip(1).Any(OtherLanguageCodes.Contains);
    }

    // the languages Starfield ships besides English
    private static readonly HashSet<string> OtherLanguageCodes = ["de", "es", "fr", "it", "ja", "pl", "ptbr", "zhhans"];

    /// <summary>True for a .wem under sound/voice/, where the game looks for dialogue audio.</summary>
    public static bool IsVoiceFilePath(string archivePath) =>
        archivePath.Replace('\\', '/').StartsWith("sound/voice/", StringComparison.OrdinalIgnoreCase)
        && archivePath.EndsWith(".wem", StringComparison.OrdinalIgnoreCase);

    public static bool ArchivePathsEqual(string a, string b) =>
        string.Equals(a.Replace('\\', '/'), b.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
