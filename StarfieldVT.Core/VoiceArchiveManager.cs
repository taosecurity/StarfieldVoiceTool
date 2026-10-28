using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Noggog;
using Serilog;

namespace StarfieldVT.Core;

public sealed record WemKey
{
    public WemKey(string masterName, string wemName)
    {
        MasterName = masterName.ToLowerInvariant();
        WemName = wemName.ToLowerInvariant();
    }

    public string MasterName { get; }
    public string WemName { get; }
}

public class VoiceArchiveManager
{
    private static readonly object SharedLock = new();
    private static VoiceArchiveManager? _shared;

    private readonly DirectoryPath _dataFolder;
    private readonly bool _includeAllModVoices;
    private readonly object _filesLock = new();
    private List<List<IArchiveFile>>? _wemFilesByArchive;

    /// <param name="includeAllModVoices">
    /// Also read voice files from archives without " - Voices" in their name, and loose files under
    /// Data/Sound/Voice. Off by default: it opens every archive, which is slower.
    /// </param>
    public VoiceArchiveManager(string dataFolderPath, bool includeAllModVoices = false)
    {
        _dataFolder = new DirectoryPath(dataFolderPath);
        _includeAllModVoices = includeAllModVoices;
    }

    /// <summary>
    /// A long-lived instance for looking up files on playback/export, so archive indexes are only read once.
    /// </summary>
    public static VoiceArchiveManager Shared(string dataFolderPath, bool includeAllModVoices = false)
    {
        lock (SharedLock)
        {
            if (_shared == null || _shared._includeAllModVoices != includeAllModVoices ||
                !_shared._dataFolder.Path.Equals(dataFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                _shared = new VoiceArchiveManager(dataFolderPath, includeAllModVoices);
            }

            return _shared;
        }
    }

    /// <summary>Forget cached archive indexes, e.g. after the game or load order changed.</summary>
    public static void ResetShared()
    {
        lock (SharedLock)
        {
            _shared = null;
        }
    }

    public List<FilePath> GetVoiceArchives() =>
        Archive.GetApplicableArchivePaths(GameRelease.Starfield, _dataFolder)
            .Where(archive => _includeAllModVoices
                ? !VoicePaths.IsOtherLanguageArchive(archive.NameWithoutExtension)
                : VoicePaths.IsEnglishVoiceArchive(archive.NameWithoutExtension))
            .ToList();

    /// <summary>
    /// Loose .wem files under Data/Sound/Voice, with paths relative to the Data folder like archive paths.
    /// Folder names are matched case-insensitively, as the game does, so this also works on Linux.
    /// </summary>
    public static List<IArchiveFile> GetLooseVoiceFiles(string dataFolderPath)
    {
        var voiceFolder = FindChildFolder(FindChildFolder(dataFolderPath, "Sound"), "Voice");
        if (voiceFolder == null) return [];

        return Directory.EnumerateFiles(voiceFolder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(dataFolderPath, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(VoicePaths.IsVoiceFilePath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => (IArchiveFile)new LooseVoiceFile(dataFolderPath, path))
            .ToList();
    }

    private static string? FindChildFolder(string? parent, string name) =>
        parent == null || !Directory.Exists(parent)
            ? null
            : Directory.EnumerateDirectories(parent)
                .FirstOrDefault(dir => Path.GetFileName(dir).Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The .wem files of each source, highest priority first: loose files (they override archives in
    /// the game, and are only read when all mod voices are included), then each archive.
    /// </summary>
    private IEnumerable<List<IArchiveFile>> ReadVoiceFileSources()
    {
        if (_includeAllModVoices)
        {
            var loose = GetLooseVoiceFiles(_dataFolder.Path);
            Log.Information("Found {Count} loose voice files", loose.Count);
            yield return loose;
        }

        foreach (var archive in GetVoiceArchives())
        {
            Log.Information("Reading voice archive {Archive}", archive.Name);
            // other archives also hold sound effects; only sound/voice files are dialogue
            yield return Archive.CreateReader(GameRelease.Starfield, archive).Files
                .Where(file => _includeAllModVoices
                    ? VoicePaths.IsVoiceFilePath(file.Path)
                    : file.Path.EndsWith(".wem", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Index every voice .wem by (plugin folder, file name). The plugin folder namespaces the files,
    /// so one map serves every plugin in the load order.
    /// </summary>
    public Dictionary<WemKey, List<WemFileReference>> BuildWemMap()
    {
        var wemDict = new Dictionary<WemKey, List<WemFileReference>>();
        var skipped = 0;

        foreach (var file in GetWemFilesByArchive().SelectMany(files => files))
        {
            var pluginFolder = VoicePaths.GetPluginFolder(file.Path);
            var voiceType = VoicePaths.GetVoiceType(file.Path);
            if (pluginFolder == null || voiceType == null)
            {
                skipped++;
                continue;
            }

            var wemKey = new WemKey(pluginFolder, Path.GetFileName(file.Path));
            if (!wemDict.TryGetValue(wemKey, out var refs))
            {
                refs = [];
                wemDict.Add(wemKey, refs);
            }

            // a loose file and an archive can hold the same path; list the line once
            if (_includeAllModVoices && refs.Any(existing => VoicePaths.ArchivePathsEqual(existing.WemPath, file.Path)))
                continue;

            refs.Add(new WemFileReference(file.Path, voiceType));
        }

        if (skipped > 0)
        {
            Log.Warning("Skipped {Count} wem files that were not under a plugin/voice type folder", skipped);
        }

        Log.Information("Indexed {Count} distinct voice files", wemDict.Count);
        return wemDict;
    }

    /// <summary>Find a voice file by its full path inside the archive, e.g. a <see cref="Models.VoiceLine.Filename"/>.</summary>
    public IArchiveFile? FindFile(string archivePath)
    {
        foreach (var files in GetWemFilesByArchive())
        {
            var match = files.FirstOrDefault(file => VoicePaths.ArchivePathsEqual(file.Path, archivePath));
            if (match != null) return match;
        }

        return null;
    }

    private List<List<IArchiveFile>> GetWemFilesByArchive()
    {
        lock (_filesLock)
        {
            return _wemFilesByArchive ??= ReadVoiceFileSources().ToList();
        }
    }
}

/// <summary>A loose voice file in the Data folder, read from disk when played or exported.</summary>
public sealed class LooseVoiceFile : IArchiveFile
{
    private readonly string _fullPath;

    public LooseVoiceFile(string dataFolderPath, string relativePath)
    {
        _fullPath = System.IO.Path.Combine(dataFolderPath, relativePath);
        Path = relativePath;
    }

    public string Path { get; }
    public uint Size => (uint)new FileInfo(_fullPath).Length;
    public byte[] GetBytes() => File.ReadAllBytes(_fullPath);
    public ReadOnlySpan<byte> GetSpan() => GetBytes();
    public ReadOnlyMemorySlice<byte> GetMemorySlice() => new(GetBytes());
    public Stream AsStream() => File.OpenRead(_fullPath);
}

public class WemFileReference
{
    public string WemPath { get; }
    public string VoiceType { get; }

    public WemFileReference(string wemPath, string voiceType)
    {
        WemPath = wemPath;
        VoiceType = voiceType;
    }
}
