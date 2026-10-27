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
    private readonly object _filesLock = new();
    private List<List<IArchiveFile>>? _wemFilesByArchive;

    public VoiceArchiveManager(string dataFolderPath)
    {
        _dataFolder = new DirectoryPath(dataFolderPath);
    }

    /// <summary>
    /// A long-lived instance for looking up files on playback/export, so archive indexes are only read once.
    /// </summary>
    public static VoiceArchiveManager Shared(string dataFolderPath)
    {
        lock (SharedLock)
        {
            if (_shared == null || !_shared._dataFolder.Path.Equals(dataFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                _shared = new VoiceArchiveManager(dataFolderPath);
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
            .Where(archive => VoicePaths.IsEnglishVoiceArchive(archive.NameWithoutExtension))
            .ToList();

    /// <summary>
    /// Index every .wem in the English voice archives by (plugin folder, file name). The plugin folder
    /// namespaces the files, so one map serves every plugin in the load order.
    /// </summary>
    public Dictionary<WemKey, List<WemFileReference>> BuildWemMap()
    {
        var wemDict = new Dictionary<WemKey, List<WemFileReference>>();
        var skipped = 0;

        foreach (var archive in GetVoiceArchives())
        {
            Log.Information("Reading voice archive {Archive}", archive.Name);
            var archiveReader = Archive.CreateReader(GameRelease.Starfield, archive);
            foreach (var file in archiveReader.Files)
            {
                if (!file.Path.EndsWith(".wem", StringComparison.OrdinalIgnoreCase)) continue;

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

                refs.Add(new WemFileReference(file.Path, voiceType));
            }
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
            return _wemFilesByArchive ??= GetVoiceArchives()
                .Select(archive => Archive.CreateReader(GameRelease.Starfield, archive).Files
                    .Where(file => file.Path.EndsWith(".wem", StringComparison.OrdinalIgnoreCase))
                    .ToList())
                .ToList();
        }
    }
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
