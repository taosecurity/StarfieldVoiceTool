using Lucene.Net.Analysis.Standard;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Util;

using StarfieldVT.Core;
using StarfieldVT.Core.Models;

namespace StarfieldVT.Core.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("svt-tests-").FullName;
    public void Dispose() => Directory.Delete(Path, recursive: true);
}

public class GameFingerprintTests
{
    [Fact]
    public void Fingerprint_IsStableAndTracksPluginsArchivesAndLoadOrder()
    {
        using var dir = new TempDir();
        var data = Path.Combine(dir.Path, "Data");
        Directory.CreateDirectory(data);
        var pluginsTxt = Path.Combine(dir.Path, "Plugins.txt");
        File.WriteAllText(Path.Combine(data, "Starfield.esm"), "a");
        File.WriteAllText(Path.Combine(data, "readme.txt"), "ignored");
        File.WriteAllText(pluginsTxt, "*Mod.esp");

        var original = GameFingerprint.Compute(data, pluginsTxt);
        Assert.Equal(original, GameFingerprint.Compute(data, pluginsTxt));

        File.WriteAllText(Path.Combine(data, "readme.txt"), "still ignored");
        Assert.Equal(original, GameFingerprint.Compute(data, pluginsTxt));

        File.WriteAllText(Path.Combine(data, "Mod - Voices_en.ba2"), "new archive");
        var withArchive = GameFingerprint.Compute(data, pluginsTxt);
        Assert.NotEqual(original, withArchive);

        File.WriteAllText(pluginsTxt, "*Other.esp");
        Assert.NotEqual(withArchive, GameFingerprint.Compute(data, pluginsTxt));
    }
}

public class LooseVoiceFileTests
{
    [Fact]
    public void GetLooseVoiceFiles_FindsWemUnderSoundVoiceInAnyCase()
    {
        using var dir = new TempDir();
        var voiceType = Path.Combine(dir.Path, "sound", "VOICE", "MyMod.esp", "npcfx");
        Directory.CreateDirectory(voiceType);
        File.WriteAllBytes(Path.Combine(voiceType, "00000001.wem"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(voiceType, "00000001.ffxanim"), "lip sync, not audio");

        var files = VoiceArchiveManager.GetLooseVoiceFiles(dir.Path);

        var file = Assert.Single(files);
        Assert.Equal("sound/VOICE/MyMod.esp/npcfx/00000001.wem", file.Path);
        Assert.Equal("MyMod.esp", VoicePaths.GetPluginFolder(file.Path));
        Assert.Equal([1, 2, 3], file.GetBytes());
        Assert.Equal(3u, file.Size);
    }

    [Fact]
    public void GetLooseVoiceFiles_IsEmptyWithoutVoiceFolder()
    {
        using var dir = new TempDir();
        Assert.Empty(VoiceArchiveManager.GetLooseVoiceFiles(dir.Path));
    }

    [Fact]
    public void Fingerprint_TracksOptionAndLooseVoiceFilesOnlyWhenEnabled()
    {
        using var dir = new TempDir();
        var data = Path.Combine(dir.Path, "Data");
        var voiceType = Path.Combine(data, "Sound", "Voice", "MyMod.esp", "npcfx");
        Directory.CreateDirectory(voiceType);
        var pluginsTxt = Path.Combine(dir.Path, "Plugins.txt");

        var off = GameFingerprint.Compute(data, pluginsTxt);
        var on = GameFingerprint.Compute(data, pluginsTxt, includeAllModVoices: true);
        Assert.NotEqual(off, on);

        File.WriteAllBytes(Path.Combine(voiceType, "00000001.wem"), [1]);
        Assert.Equal(off, GameFingerprint.Compute(data, pluginsTxt));
        Assert.NotEqual(on, GameFingerprint.Compute(data, pluginsTxt, includeAllModVoices: true));
    }
}

public class CacheManagerTests
{
    private static List<Master> SampleTree() =>
    [
        new Master("Starfield.esm",
        [
            new VoiceType
            {
                FromMaster = "Starfield.esm",
                EditorId = "npcmboe",
                VoiceLines = [new VoiceLine("sound/voice/starfield.esm/npcmboe/00000001.wem", "Hello", "Starfield.esm", "npcmboe")]
            }
        ])
    ];

    [Fact]
    public void Cache_RoundTripsWhenFingerprintMatches()
    {
        using var dir = new TempDir();
        var manager = new VoiceLineTreeCacheManager(Path.Combine(dir.Path, "cache.json"));
        manager.SaveCurrentTree(SampleTree(), "fp1");

        var loaded = manager.TryToLoadCache("fp1");

        Assert.NotNull(loaded);
        var line = Assert.Single(Assert.Single(Assert.Single(loaded).VoiceTypes).VoiceLines);
        Assert.Equal("Hello", line.Dialogue);
    }

    [Fact]
    public void Cache_IsStaleWhenFingerprintDiffers()
    {
        using var dir = new TempDir();
        var manager = new VoiceLineTreeCacheManager(Path.Combine(dir.Path, "cache.json"));
        manager.SaveCurrentTree(SampleTree(), "fp1");

        Assert.Null(manager.TryToLoadCache("fp2"));
    }

    [Fact]
    public void Cache_IsStaleWhenWrittenByAnotherVersion()
    {
        using var dir = new TempDir();
        var cachePath = Path.Combine(dir.Path, "cache.json");
        File.WriteAllText(cachePath, """{"Masters":[],"Version":"0.5.0.0","GameFingerprint":"fp1"}""");

        Assert.Null(new VoiceLineTreeCacheManager(cachePath).TryToLoadCache("fp1"));
    }

    [Fact]
    public void Cache_IgnoresCorruptFile()
    {
        using var dir = new TempDir();
        var cachePath = Path.Combine(dir.Path, "cache.json");
        File.WriteAllText(cachePath, "{ not json");

        Assert.Null(new VoiceLineTreeCacheManager(cachePath).TryToLoadCache("fp1"));
    }
}

public class SearchQueryTests
{
    private static QueryParser Parser() =>
        new(LuceneVersion.LUCENE_48, "dialogue", new StandardAnalyzer(LuceneVersion.LUCENE_48));

    [Theory]
    [InlineData("\"")]
    [InlineData("(")]
    [InlineData("?")]
    [InlineData("*ship")]
    [InlineData("where are you [")]
    [InlineData("AND")]
    public void ParseUserQuery_DoesNotThrowOnIncompleteSyntax(string input)
    {
        Assert.NotNull(LuceneManager.ParseUserQuery(Parser(), input));
    }

    [Fact]
    public void ParseUserQuery_KeepsValidLuceneSyntax()
    {
        // a phrase stays a phrase ("on" is a stop word, so it becomes a gap)
        var phrase = LuceneManager.ParseUserQuery(Parser(), "\"captain on deck\"");
        Assert.IsType<Lucene.Net.Search.PhraseQuery>(phrase);
        Assert.Equal("dialogue:\"captain ? deck\"", phrase.ToString());

        var wildcard = LuceneManager.ParseUserQuery(Parser(), "capt*");
        Assert.IsType<Lucene.Net.Search.PrefixQuery>(wildcard);
    }
}
