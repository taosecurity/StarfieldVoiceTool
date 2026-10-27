using System.Diagnostics;
using System.Runtime.InteropServices;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda.Strings;

using Serilog;

using StarfieldVT.Core.Models;

namespace StarfieldVT.Core
{
    using VoiceType = Models.VoiceType;

    public class DialogueTreeBuilder
    {
        // reporting every quest floods the UI thread with thousands of updates
        private const int ProgressInterval = 25;

        private readonly VoiceLineTreeCacheManager _voiceLineTreeCacheManager = new VoiceLineTreeCacheManager();
        private readonly LuceneManager _luceneManager = LuceneManager.Instance();
        private int _lineCount = 0;
        private int _unvoicedCount = 0;

        public List<Master> BuildTree(string dataFolderPath, string gameFingerprint,
            IProgress<EsmLoadingProgress> progress, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                EnsureLinuxPluginsFile(dataFolderPath);

            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .WithTargetDataFolder(dataFolderPath)
                .Build();

            progress.Report(new EsmLoadingProgress { Message = "Reading voice archives..." });
            var wemMap = new VoiceArchiveManager(dataFolderPath).BuildWemMap();

            var plugins = env.LoadOrder.ListedOrder.Where(esm => esm is { Enabled: true, ExistsOnDisk: true }).ToList();
            var tree = new List<Master>();
            for (var i = 0; i < plugins.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var master = BuildMaster(plugins[i], i + 1, plugins.Count, wemMap, progress, cancellationToken);
                if (master != null)
                {
                    tree.Add(master);
                }
            }

            // commit the index before writing the cache, so a cache never exists without its index
            _luceneManager.CommitWrites();
            cancellationToken.ThrowIfCancellationRequested();
            Log.Information("Saving generated tree to cache");
            _voiceLineTreeCacheManager.SaveCurrentTree(tree, gameFingerprint);

            Log.Information("Found {LineCount} lines in {Seconds} seconds", _lineCount, stopwatch.Elapsed.TotalSeconds);
            return tree;
        }

        private Master? BuildMaster(IModListingGetter<IStarfieldModGetter> esm, int pluginNumber, int pluginCount,
            Dictionary<WemKey, List<WemFileReference>> wemMap, IProgress<EsmLoadingProgress> progress,
            CancellationToken cancellationToken)
        {
            Log.Information("Loading plugin {FileName}", esm.FileName);
            _unvoicedCount = 0;
            var linesByVoiceType = new Dictionary<string, List<VoiceLine>>();
            var quests = esm.Mod?.Quests.ToList() ?? [];

            for (var questNumber = 1; questNumber <= quests.Count; questNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var quest = quests[questNumber - 1];
                Log.Debug("Parsing quest {EditorId} in {FileName}", quest.EditorID, esm.FileName);

                if (questNumber == 1 || questNumber % ProgressInterval == 0 || questNumber == quests.Count)
                {
                    progress.Report(new EsmLoadingProgress
                    {
                        EsmName = esm.FileName,
                        PluginNumber = pluginNumber,
                        PluginCount = pluginCount,
                        Num = questNumber,
                        Total = quests.Count
                    });
                }

                foreach (var dt in quest.DialogTopics)
                {
                    Log.Debug("Parsing dialogue {Name} in {FileName}", dt.Name, esm.FileName);
                    foreach (var resp in dt.Responses)
                    {
                        // responses without TROTs first, as before
                        foreach (var innerResp in resp.Responses.OrderBy(innerResp => innerResp.TROTs.Count > 0))
                        {
                            ProcessResponse(innerResp, esm, linesByVoiceType, wemMap);
                        }
                    }
                }
            }

            if (_unvoicedCount > 0)
            {
                Log.Information(
                    "{Count} dialogue responses in {FileName} have no voice file (normal for player lines and unvoiced text)",
                    _unvoicedCount, esm.FileName);
            }

            var voiceTypes = linesByVoiceType.Select(voiceType => new VoiceType
            {
                FromMaster = esm.FileName,
                EditorId = voiceType.Key,
                VoiceLines = voiceType.Value
            }).OrderBy(voiceType => voiceType.EditorId).ToList();

            return voiceTypes.Count > 0 ? new Master(esm.FileName, voiceTypes) : null;
        }

        private static void EnsureLinuxPluginsFile(string dataFolderPath)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Environment.SetEnvironmentVariable("LocalAppData", localAppData);

            var pluginsDir = Path.Combine(localAppData, "Starfield");
            Directory.CreateDirectory(pluginsDir);
            var pluginsPath = Path.Combine(pluginsDir, "Plugins.txt");

            if (!File.Exists(pluginsPath))
            {
                Log.Information("Creating Plugins.txt for Linux at {Path}", pluginsPath);
                var plugins = new[] { "*.esm", "*.esp", "*.esl" }
                    .SelectMany(p => Directory.EnumerateFiles(dataFolderPath, p))
                    .Select(f => "*" + Path.GetFileName(f));
                File.WriteAllLines(pluginsPath, plugins);
            }
        }

        private void ProcessResponse(IDialogResponseGetter innerResp, IModListingGetter<IStarfieldModGetter> esm,
            Dictionary<string, List<VoiceLine>> linesByVoiceType, Dictionary<WemKey, List<WemFileReference>> wemMap)
        {
            var wemFileName = VoicePaths.GetArchiveWemFileName(innerResp.WEMFile);
            if (!wemMap.TryGetValue(new WemKey(esm.FileName, wemFileName), out var wemFileRefs))
            {
                // most responses without audio are player lines (the player is unvoiced), so this is expected
                Log.Debug("No voice file {WemFile} for {FileName}, continuing on.", wemFileName, esm.FileName);
                _unvoicedCount++;
                return;
            }

            if (!innerResp.Text.TryLookup(Language.English, out var innerRespText) || innerRespText == null)
            {
                return;
            }

            foreach (var wemFile in wemFileRefs)
            {
                var voiceLine = new VoiceLine(wemFile.WemPath, innerRespText, esm.FileName, wemFile.VoiceType);
                if (!linesByVoiceType.TryGetValue(wemFile.VoiceType, out var lines))
                {
                    lines = [];
                    linesByVoiceType.Add(wemFile.VoiceType, lines);
                }

                lines.Add(voiceLine);
                _luceneManager.AddDocumentWithoutCommitting(voiceLine);
                _lineCount++;
            }
        }
    }
}
