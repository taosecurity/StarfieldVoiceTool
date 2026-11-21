using System.Globalization;

using StarfieldVT.Core.Models;
using StarfieldVT.Core.Sounds;

namespace StarfieldVT.Core.Export;

/// <summary>
/// Builds the output paths and list.csv rows for a bulk export. Names come from game and mod
/// archives, so every folder and file name part is cleaned, and the final path must stay inside
/// the export folder.
/// </summary>
public static class ExportLayout
{
    /// <summary>The list.csv columns for sounds (File first).</summary>
    public static readonly IReadOnlyList<string> SoundColumns =
        ["File", "ID", "Name", "Group", "Folder", "Events", "Bank", "Source", "Replaces"];

    /// <summary>The list.csv columns for voice lines (File first).</summary>
    public static readonly IReadOnlyList<string> VoiceColumns =
        ["File", "Plugin", "Voice Type", "Game File", "Dialogue"];

    /// <summary>The folder for sounds that no sound bank names.</summary>
    public const string UnnamedFolder = "Unnamed";

    private static readonly HashSet<char> Invalid =
        Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*']).ToHashSet();

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Replace characters Windows doesn't allow in a file name (and control characters) with "_",
    /// then trim spaces and dots. May return "" (callers choose a fallback).
    /// </summary>
    public static string CleanName(string? name) =>
        new string((name ?? "").Select(c => Invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim(' ', '.');

    /// <summary>
    /// A cleaned folder or file name part that is never empty, ".", ".." or a Windows device name
    /// (CON, NUL, COM1, ... also with an extension such as "CON.wav").
    /// </summary>
    public static string SafePart(string? name, string fallback = "_")
    {
        var clean = CleanName(name);
        if (clean.Length == 0) return fallback;
        var stem = clean.Split('.')[0].TrimEnd(' ');
        return Reserved.Contains(stem) ? "_" + clean : clean;
    }

    /// <summary>Split a game path such as "Hand\Microgun" into cleaned parts, dropping empty ones.</summary>
    public static IEnumerable<string> FolderParts(string? folder) =>
        (folder ?? "").Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanName)
            .Where(p => p.Length > 0)
            .Select(p => SafePart(p));

    /// <summary>
    /// The full path of <paramref name="relativePath"/> below <paramref name="root"/>. Throws
    /// <see cref="InvalidOperationException"/> if the result would land outside the root.
    /// </summary>
    public static string ResolveInside(string root, string relativePath)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"The export path \"{relativePath}\" is not relative to the export folder.");
        }

        var full = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var prefix = fullRoot + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The export path \"{relativePath}\" would be written outside the export folder {fullRoot}.");
        }

        return full;
    }

    /// <summary>
    /// Export items for sounds. With <paramref name="keepFolders"/>: "&lt;Group&gt;\&lt;Folder&gt;\&lt;Name&gt;_&lt;Id&gt;.wav"
    /// from the Wwise ShortName, and "Unnamed\&lt;Id&gt;.wav" for sounds no bank names; otherwise one flat folder.
    /// </summary>
    public static List<ExportItem<SoundEffect>> ForSounds(IEnumerable<SoundEffect> sounds, bool keepFolders)
    {
        var names = new UniqueNames();
        var items = new List<ExportItem<SoundEffect>>();
        foreach (var sound in sounds)
        {
            var parts = new List<string>();
            if (keepFolders)
            {
                if (sound.ShortName == null)
                {
                    parts.Add(UnnamedFolder);
                }
                else
                {
                    parts.Add(SafePart(sound.Group, "Other"));
                    parts.AddRange(FolderParts(sound.Folder));
                }
            }

            parts.Add(SoundCatalog.ExportFileName(sound));
            var path = names.Claim(Path.Combine([.. parts]));
            items.Add(new ExportItem<SoundEffect>(path,
            [
                sound.Id.ToString(CultureInfo.InvariantCulture),
                sound.Name,
                sound.Group,
                sound.Folder,
                string.Join("; ", sound.Events),
                sound.Bank ?? "",
                sound.SourceText,
                string.Join("; ", sound.Replaces),
            ], sound, $"sound {sound.Id} from {sound.SourceText}"));
        }

        return items;
    }

    /// <summary>The export file name for a voice line: "&lt;VoiceType&gt;_&lt;file&gt;.wav", as the single-line Save dialog suggests.</summary>
    public static string VoiceFileName(VoiceLine line)
    {
        var file = line.Filename.Split(['\\', '/']).LastOrDefault() ?? "";
        var stem = CleanName(Path.GetFileNameWithoutExtension(file));
        var voiceType = CleanName(line.VoiceType);
        var name = voiceType.Length == 0 ? stem : stem.Length == 0 ? voiceType : $"{voiceType}_{stem}";
        return SafePart(name, "voice") + ".wav";
    }

    /// <summary>
    /// Export items for voice lines. With <paramref name="keepFolders"/>:
    /// "&lt;Plugin&gt;\&lt;VoiceType&gt;\&lt;VoiceType&gt;_&lt;file&gt;.wav"; otherwise one flat folder.
    /// </summary>
    public static List<ExportItem<VoiceLine>> ForVoices(IEnumerable<VoiceLine> lines, bool keepFolders)
    {
        var names = new UniqueNames();
        var items = new List<ExportItem<VoiceLine>>();
        foreach (var line in lines)
        {
            var file = VoiceFileName(line);
            var path = keepFolders
                ? Path.Combine(SafePart(line.ModName, "Unknown plugin"), SafePart(line.VoiceType, "Unknown voice type"), file)
                : file;
            path = names.Claim(path);
            items.Add(new ExportItem<VoiceLine>(path,
                [line.ModName, line.VoiceType, line.Filename, line.Dialogue ?? ""],
                line, $"voice line {line.Filename} from {line.ModName}"));
        }

        return items;
    }

    /// <summary>Hands out paths, adding "_2", "_3", ... before the extension when one is already taken (case-insensitive, as on Windows).</summary>
    private sealed class UniqueNames
    {
        private readonly HashSet<string> _taken = new(StringComparer.OrdinalIgnoreCase);

        public string Claim(string path)
        {
            if (_taken.Add(path)) return path;
            var dir = Path.GetDirectoryName(path) ?? "";
            var stem = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (var n = 2; ; n++)
            {
                var candidate = Path.Combine(dir, $"{stem}_{n}{ext}");
                if (_taken.Add(candidate)) return candidate;
            }
        }
    }
}
