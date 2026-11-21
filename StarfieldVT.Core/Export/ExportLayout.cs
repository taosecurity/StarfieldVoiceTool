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
        ["File", "ID", "Name", "Group", "Wwise Group", "Folder", "Events", "Bank", "Source", "Replaces"];

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
    /// (CON, NUL, COM1, ... also with an extension such as "CON.wav"), and never starts with a
    /// spreadsheet formula character (= + - @), so list.csv's File column needs no formula guard and
    /// always matches the file on disk.
    /// </summary>
    public static string SafePart(string? name, string fallback = "_")
    {
        var clean = CleanName(name);
        if (clean.Length == 0) return fallback;
        var stem = clean.Split('.')[0].TrimEnd(' ');
        return Reserved.Contains(stem) || clean[0] is '=' or '+' or '-' or '@' ? "_" + clean : clean;
    }

    /// <summary>
    /// The list.csv columns that say which item a row is (the rest, such as Dialogue or Events, may
    /// change with a game update): ID and Source for sounds, Plugin and Game File for voices, else
    /// every column but File.
    /// </summary>
    public static int[] IdentityColumns(IReadOnlyList<string> header)
    {
        int[] Find(params string[] names) => names.Select(n => header.ToList().IndexOf(n)).ToArray();
        if (header.SequenceEqual(SoundColumns)) return Find("ID", "Source");
        if (header.SequenceEqual(VoiceColumns)) return Find("Plugin", "Game File");
        return Enumerable.Range(1, Math.Max(0, header.Count - 1)).ToArray();
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
        // a drive root such as "D:\" keeps its separator after the trim
        var prefix = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        // Linux file names are case-sensitive, so "/tmp/OUT" is a different folder from "/tmp/out"
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(prefix, comparison))
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
        var items = new List<ExportItem<SoundEffect>>();
        var tags = new List<string>();
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

            parts.Add(SafePart(SoundCatalog.ExportFileName(sound)));
            var path = Path.Combine([.. parts]);
            tags.Add(Path.GetFileNameWithoutExtension(sound.SourceText));
            items.Add(new ExportItem<SoundEffect>(path,
            [
                sound.Id.ToString(CultureInfo.InvariantCulture),
                sound.Name,
                SoundCatalog.GroupLabel(sound),
                sound.Group,
                sound.Folder,
                string.Join("; ", sound.Events),
                sound.Bank ?? "",
                sound.SourceText,
                string.Join("; ", sound.Replaces),
            ], sound, $"sound {sound.Id} from {sound.SourceText}"));
        }

        return SettleClashes(items, tags);
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
        var items = new List<ExportItem<VoiceLine>>();
        var tags = new List<string>();
        foreach (var line in lines)
        {
            var file = VoiceFileName(line);
            var path = keepFolders
                ? Path.Combine(SafePart(line.ModName, "Unknown plugin"), SafePart(line.VoiceType, "Unknown voice type"), file)
                : file;
            tags.Add(Path.GetFileNameWithoutExtension(line.ModName ?? ""));
            items.Add(new ExportItem<VoiceLine>(path,
                [line.ModName ?? "", line.VoiceType, line.Filename, line.Dialogue ?? ""],
                line, $"voice line {line.Filename} from {line.ModName}"));
        }

        return SettleClashes(items, tags);
    }

    /// <summary>
    /// Give every item in this run a path of its own, the same whatever else is exported with it. (Across
    /// runs into one folder, <see cref="BulkExporter"/> checks a kept file against the earlier list.csv.) A path
    /// that only one item wants stays as it is. When several want the same path (case-insensitive, as
    /// on Windows), each gets its source added ("Boom_5_Mod.wav", "NPCFAstrid_x_ModB.wav"); if that
    /// still clashes, a short hash of the item's own game path and source ("_1a2b3c4d"). Only items
    /// that are the same in every way get "_2", "_3".
    /// </summary>
    private static List<ExportItem<T>> SettleClashes<T>(List<ExportItem<T>> items, List<string> tags)
    {
        var wanted = items.GroupBy(i => i.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        if (wanted.Values.All(n => n == 1)) return items;

        var paths = new string?[items.Count];
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < items.Count; i++)
        {
            if (wanted[items[i].RelativePath] == 1)
            {
                paths[i] = items[i].RelativePath;
                taken.Add(items[i].RelativePath);
            }
        }

        // the source tag, unless two clashing items share it too
        var tagged = new string[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            if (paths[i] == null) tagged[i] = WithSuffix(items[i].RelativePath, SafePart(tags[i], "source"));
        }

        var taggedCount = Enumerable.Range(0, items.Count).Where(i => paths[i] == null)
            .GroupBy(i => tagged[i], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < items.Count; i++)
        {
            if (paths[i] != null) continue;
            var candidate = taggedCount[tagged[i]] == 1 && !taken.Contains(tagged[i])
                ? tagged[i]
                : WithSuffix(items[i].RelativePath, StableHash(items[i].Description));
            for (var n = 2; !taken.Add(candidate); n++)
            {
                candidate = WithSuffix(items[i].RelativePath, StableHash(items[i].Description) + "_" + n);
            }

            paths[i] = candidate;
        }

        return items.Select((item, i) => item with { RelativePath = paths[i]! }).ToList();
    }

    /// <summary>"dir\name.wav" with "_suffix" before the extension: "dir\name_suffix.wav".</summary>
    public static string WithSuffix(string path, string suffix) =>
        Path.Combine(Path.GetDirectoryName(path) ?? "", $"{Path.GetFileNameWithoutExtension(path)}_{suffix}{Path.GetExtension(path)}");

    /// <summary>Eight hex digits of FNV-1a over the lower-case text: the same in every run (string.GetHashCode isn't).</summary>
    public static string StableHash(string text)
    {
        var hash = 2166136261u;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(text.ToLowerInvariant()))
        {
            hash = (hash ^ b) * 16777619u;
        }

        return hash.ToString("x8", CultureInfo.InvariantCulture);
    }
}
