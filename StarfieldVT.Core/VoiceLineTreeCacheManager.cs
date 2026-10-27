using System.IO;
using System.Reflection;
using System.Text.Json;

using Serilog;

using StarfieldVT.Core.Filesystem;
using StarfieldVT.Core.Models;

namespace StarfieldVT.Core
{
    public class VoiceLineTreeCacheManager
    {
        private const string CacheFilename = "cache.json";
        private readonly string _cachePath;

        public VoiceLineTreeCacheManager() : this(Path.Combine(AppDataFolder.GetAppDataFolder(), CacheFilename))
        {
        }

        public VoiceLineTreeCacheManager(string cachePath)
        {
            _cachePath = cachePath;
        }

        private static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version();

        public void SaveCurrentTree(List<Master> tree, string gameFingerprint)
        {
            Log.Information("Writing cache file to {CachePath}", _cachePath);

            var cacheFile = new CacheFile { Masters = tree, Version = CurrentVersion, GameFingerprint = gameFingerprint };
            var directory = Path.GetDirectoryName(_cachePath);
            if (directory != null) Directory.CreateDirectory(directory);

            // write to a temp file first so a crash mid-write can't leave a truncated cache behind
            var tempPath = _cachePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(cacheFile));
            File.Move(tempPath, _cachePath, overwrite: true);
        }

        private static bool IsCacheStale(CacheFile cacheFile, string gameFingerprint)
        {
            if (cacheFile.Version != CurrentVersion)
            {
                Log.Information("Cache file is from version {CacheVersion} (running {CurrentVersion}), rebuilding the voice line tree.",
                    cacheFile.Version, CurrentVersion);
                return true;
            }

            if (cacheFile.GameFingerprint != gameFingerprint)
            {
                Log.Information("Game files, data folder or load order changed since the cache was built, rebuilding the voice line tree.");
                return true;
            }

            return false;
        }

        public List<Master>? TryToLoadCache(string gameFingerprint)
        {
            if (!File.Exists(_cachePath)) return null;
            Log.Information("Loading cache from {CachePath}", _cachePath);

            try
            {
                var cache = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(_cachePath));
                if (cache == null || IsCacheStale(cache, gameFingerprint))
                {
                    return null;
                }

                return cache.Masters;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load cache from {CachePath} and will invalidate + reload instead", _cachePath);
            }

            return null;
        }

        public void BustCache()
        {
            Log.Information("Deleting cache file at {CachePath}", _cachePath);

            if (File.Exists(_cachePath))
            {
                File.Delete(_cachePath);
            }
        }
    }
}
