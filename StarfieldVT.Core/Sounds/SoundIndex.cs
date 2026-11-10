using System.Diagnostics;
using System.Globalization;

using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;

using Noggog;

using Serilog;

namespace StarfieldVT.Core.Sounds;

/// <summary>A place the game reads sounds from: one archive, or the loose files in the Data folder.</summary>
public sealed class SoundSource
{
    public const string LooseName = "Data folder";

    public SoundSource(string name, bool isBaseGame, IReadOnlyList<IArchiveFile> files)
    {
        Name = name;
        IsBaseGame = isBaseGame;
        Files = files;
    }

    public string Name { get; }
    public bool IsBaseGame { get; }

    /// <summary>The source's files under sound/soundbanks.</summary>
    public IReadOnlyList<IArchiveFile> Files { get; }
}

public sealed class SoundIndexResult
{
    public required List<SoundEffect> Sounds { get; init; }

    /// <summary>The file behind each sound, for playback and export (not cached).</summary>
    public required Dictionary<uint, IArchiveFile> Files { get; init; }

    public int SourcesWithSounds { get; init; }
    public int Unnamed { get; init; }

    /// <summary>Streamed sounds a bank names but no source holds.</summary>
    public int NamedButMissing { get; init; }
}

/// <summary>
/// Lists every Wwise sound the game can load, with names and events from the banks' JSON files.
/// Sources are read in the game's order (archives as Mutagen orders them, then loose files); when
/// two hold the same sound ID, the later one wins, as in the game.
/// </summary>
/// <remarks>
/// Names come from every bank's own &lt;bankId&gt;.json, which vanilla and mod banks both ship. The
/// combined soundbanksinfo.json is only a fallback for banks without one: a mod can ship its own
/// copy of that file, and trusting the last copy alone would let it hide or rename vanilla sounds.
/// </remarks>
public static class SoundIndex
{
    private const string SoundbanksFolder = "sound/soundbanks/";

    public static SoundIndexResult Build(string dataFolderPath) => Build(ReadSources(dataFolderPath));

    public static SoundIndexResult Build(IEnumerable<SoundSource> sources)
    {
        var timer = Stopwatch.StartNew();
        var winners = new Dictionary<uint, (SoundSource Source, IArchiveFile File)>();
        var replaced = new Dictionary<uint, List<string>>();
        var perBank = new Dictionary<uint, SoundBankInfo>();
        var combinedFromMods = new List<SoundbankJsonFile>();
        var combinedFromGame = new List<SoundbankJsonFile>();
        var sourcesWithSounds = 0;

        foreach (var source in sources)
        {
            var wems = 0;
            var overrides = 0;
            foreach (var file in source.Files)
            {
                var name = Path.GetFileName(NormalizePath(file.Path));
                var stem = Path.GetFileNameWithoutExtension(name);
                if (name.EndsWith(".wem", StringComparison.OrdinalIgnoreCase) && TryParseId(stem, out var id))
                {
                    wems++;
                    if (winners.TryGetValue(id, out var earlier))
                    {
                        overrides++;
                        if (!replaced.TryGetValue(id, out var list)) replaced[id] = list = [];
                        list.Add(earlier.Source.Name);
                        Log.Verbose("Sound {Id} in {Source} replaces the one in {Earlier}", id, source.Name, earlier.Source.Name);
                    }

                    winners[id] = (source, file);
                }
                else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var isCombined = name.Equals("soundbanksinfo.json", StringComparison.OrdinalIgnoreCase);
                    if (!isCombined && !TryParseId(stem, out _)) continue; // e.g. plugininfo.json

                    var parsed = TryParse(source, file);
                    if (parsed == null) continue;
                    if (isCombined) (source.IsBaseGame ? combinedFromGame : combinedFromMods).Add(parsed);
                    else foreach (var bank in parsed.Banks) perBank[bank.Id] = bank;
                }
            }

            if (wems > 0) sourcesWithSounds++;
            if (wems > 0 || overrides > 0)
            {
                Log.Debug("{Source}: {Count} sounds, {Overrides} replace earlier sources", source.Name, wems, overrides);
            }
        }

        // Combined files are metadata only (the game plays .bnk/.wem). A mod's copy is usually a
        // leftover from its Wwise project, so it only fills gaps; the base game's copies win over it,
        // and each bank's own file wins over any combined copy.
        var combined = combinedFromMods.Concat(combinedFromGame).ToList();
        var banks = new Dictionary<uint, SoundBankInfo>();
        foreach (var bank in combined.SelectMany(file => file.Banks)) banks[bank.Id] = bank;
        foreach (var (id, bank) in perBank) banks[id] = bank;

        var names = new Dictionary<uint, string>();
        var bankOf = new Dictionary<uint, string>();
        foreach (var file in combined.SelectMany(c => c.StreamedFiles).Where(f => f.ShortName != null))
        {
            names[file.Id] = file.ShortName!;
        }

        var events = new Dictionary<uint, SortedSet<string>>();
        var listed = new HashSet<uint>();
        foreach (var bank in banks.Values)
        {
            foreach (var file in bank.Files)
            {
                // banks often list a sound by ID only; the name then comes from another entry
                if (file.ShortName != null) names[file.Id] = file.ShortName;
                bankOf[file.Id] = bank.Name;
                if (!file.InMemory) listed.Add(file.Id);
            }

            foreach (var ev in bank.Events)
            {
                foreach (var soundId in ev.SoundIds)
                {
                    if (!events.TryGetValue(soundId, out var set)) events[soundId] = set = new SortedSet<string>(StringComparer.Ordinal);
                    set.Add(ev.Name);
                }
            }
        }

        var sounds = new List<SoundEffect>(winners.Count);
        var unnamed = 0;
        foreach (var (id, (source, file)) in winners)
        {
            var sound = new SoundEffect
            {
                Id = id,
                Source = source.Name,
                FilePath = NormalizePath(file.Path),
                FromMod = !source.IsBaseGame,
                Replaces = replaced.TryGetValue(id, out var r) ? r : [],
                Events = events.TryGetValue(id, out var e) ? e.ToList() : [],
            };

            sound.Bank = bankOf.GetValueOrDefault(id);
            if (names.TryGetValue(id, out var shortName))
            {
                SetName(sound, shortName);
            }
            else
            {
                unnamed++;
                sound.Name = id.ToString(CultureInfo.InvariantCulture);
                sound.Group = "Other";
            }

            sounds.Add(sound);
        }

        sounds.Sort((a, b) =>
        {
            var byGroup = string.Compare(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
            return byGroup != 0 ? byGroup : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        var missing = listed.Count(id => !winners.ContainsKey(id));
        Log.Information(
            "Found {Count} sounds ({ModCount} from mods, {Unnamed} without a name) in {Sources} sources in {Seconds:0.0} seconds",
            sounds.Count, sounds.Count(s => s.FromMod), unnamed, sourcesWithSounds, timer.Elapsed.TotalSeconds);
        if (missing > 0)
        {
            Log.Debug("{Count} streamed sounds named in banks have no .wem in any source", missing);
        }

        return new SoundIndexResult
        {
            Sounds = sounds,
            Files = winners.ToDictionary(pair => pair.Key, pair => pair.Value.File),
            SourcesWithSounds = sourcesWithSounds,
            Unnamed = unnamed,
            NamedButMissing = missing,
        };
    }

    /// <summary>Every archive the game loads, in order, then the loose Data/Sound/Soundbanks files.</summary>
    public static List<SoundSource> ReadSources(string dataFolderPath)
    {
        var sources = new List<SoundSource>();
        foreach (var archive in Archive.GetApplicableArchivePaths(GameRelease.Starfield, new DirectoryPath(dataFolderPath)))
        {
            try
            {
                var files = Archive.CreateReader(GameRelease.Starfield, archive).Files
                    .Where(file => IsSoundbankFile(file.Path))
                    .ToList();
                if (files.Count > 0)
                {
                    sources.Add(new SoundSource(archive.Name, IsBaseGameArchive(archive.Name), files));
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, "Could not read archive {Archive}; its sounds are skipped", archive.Path);
            }
        }

        var loose = ReadLooseFiles(dataFolderPath);
        if (loose.Count > 0)
        {
            Log.Information("Found {Count} loose files in Data/Sound/Soundbanks", loose.Count);
            sources.Add(new SoundSource(SoundSource.LooseName, false, loose));
        }

        return sources;
    }

    private static List<IArchiveFile> ReadLooseFiles(string dataFolderPath)
    {
        var folder = VoiceArchiveManager.FindChildFolder(VoiceArchiveManager.FindChildFolder(dataFolderPath, "Sound"), "Soundbanks");
        if (folder == null) return [];

        return Directory.EnumerateFiles(folder)
            .Select(file => Path.GetRelativePath(dataFolderPath, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(IsSoundbankFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => (IArchiveFile)new LooseVoiceFile(dataFolderPath, path))
            .ToList();
    }

    /// <summary>A .wem or .json directly in sound/soundbanks (the folder Wwise loads by ID).</summary>
    public static bool IsSoundbankFile(string path)
    {
        var p = NormalizePath(path);
        if (!p.StartsWith(SoundbanksFolder, StringComparison.OrdinalIgnoreCase)) return false;
        var rest = p[SoundbanksFolder.Length..];
        return !rest.Contains('/') &&
               (rest.EndsWith(".wem", StringComparison.OrdinalIgnoreCase) || rest.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsBaseGameArchive(string archiveName) =>
        archiveName.StartsWith("Starfield - ", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static bool TryParseId(string text, out uint id) =>
        uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id);

    private static void SetName(SoundEffect sound, string shortName)
    {
        sound.ShortName = shortName;
        var parts = shortName.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        sound.Name = Path.GetFileNameWithoutExtension(parts.Length > 0 ? parts[^1] : shortName);
        sound.Group = parts.Length > 1 ? parts[0] : "Other";
        sound.Folder = parts.Length > 2 ? string.Join('\\', parts[1..^1]) : "";
    }

    private static SoundbankJsonFile? TryParse(SoundSource source, IArchiveFile file)
    {
        try
        {
            return SoundbankJsonFile.Parse(file.GetBytes());
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException or IOException)
        {
            Log.Warning("Skipped {Path} in {Source}: not a readable Wwise bank JSON ({Error})", file.Path, source.Name, e.Message);
            return null;
        }
    }
}
