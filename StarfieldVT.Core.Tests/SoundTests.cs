using System.Buffers.Binary;
using System.Text;

using Mutagen.Bethesda.Archives;

using Noggog;

using StarfieldVT.Core.Sounds;

namespace StarfieldVT.Core.Tests;

/// <summary>Synthetic Wwise files for the sound tests; no game files are used or stored.</summary>
internal static class FakeWwise
{
    /// <summary>A Wwise-style .wem: RIFF, a 24-byte fmt chunk, a JUNK chunk, then data.</summary>
    public static byte[] Wem(ushort tag, ushort channels, int rate, byte[] data, ushort bits = 16, uint channelConfig = 0)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(0);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(24);
        w.Write(tag);
        w.Write(channels);
        w.Write(rate);
        w.Write(rate * channels * bits / 8);
        w.Write((ushort)(channels * bits / 8));
        w.Write(bits);
        w.Write((ushort)6);
        w.Write((ushort)0);
        w.Write(channelConfig);
        w.Write("JUNK"u8);
        w.Write(4);
        w.Write(0);
        w.Write("data"u8);
        w.Write(data.Length);
        w.Write(data);
        if (data.Length % 2 == 1) w.Write((byte)0);
        w.Flush();
        var bytes = ms.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length - 8);
        return bytes;
    }

    public static byte[] Json(string json, bool bom = false)
    {
        var body = Encoding.UTF8.GetBytes(json);
        return bom ? [0xEF, 0xBB, 0xBF, .. body] : body;
    }
}

internal sealed class MemoryFile(string path, byte[] bytes) : IArchiveFile
{
    public string Path { get; } = path;
    public uint Size => (uint)bytes.Length;
    public byte[] GetBytes() => bytes;
    public ReadOnlySpan<byte> GetSpan() => bytes;
    public ReadOnlyMemorySlice<byte> GetMemorySlice() => new(bytes);
    public Stream AsStream() => new MemoryStream(bytes, false);
}

public class WwisePcmTests
{
    private static (ushort Format, ushort Channels, int Rate, ushort Bits, int FmtSize, byte[] Data) ReadWav(byte[] wav)
    {
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(wav.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(4)));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
        var fmtSize = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(16));
        var f = wav.AsSpan(20);
        var dataAt = 20 + fmtSize;
        Assert.Equal("data", Encoding.ASCII.GetString(wav, dataAt, 4));
        var dataLength = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(dataAt + 4));
        return (BinaryPrimitives.ReadUInt16LittleEndian(f), BinaryPrimitives.ReadUInt16LittleEndian(f[2..]),
            BinaryPrimitives.ReadInt32LittleEndian(f[4..]), BinaryPrimitives.ReadUInt16LittleEndian(f[14..]), fmtSize,
            wav.AsSpan(dataAt + 8, dataLength).ToArray());
    }

    private static byte[] Convert(byte[] wem)
    {
        using var ms = new MemoryStream();
        WwisePcm.ToWav(wem, ms);
        return ms.ToArray();
    }

    [Fact]
    public void StereoPcmBecomesPlainWavWithTheSameSamples()
    {
        var samples = Enumerable.Range(0, 400).Select(i => (byte)i).ToArray();
        var wav = Convert(FakeWwise.Wem(WemFormat.WwisePcm, 2, 48000, samples, channelConfig: 0x3102));

        var (format, channels, rate, bits, fmtSize, data) = ReadWav(wav);
        Assert.Equal(1, format);
        Assert.Equal(2, channels);
        Assert.Equal(48000, rate);
        Assert.Equal(16, bits);
        Assert.Equal(16, fmtSize);
        Assert.Equal(samples, data);
    }

    [Fact]
    public void PartialLastFrameIsDropped()
    {
        var samples = new byte[403]; // stereo 16-bit frames are 4 bytes
        var (_, _, _, _, _, data) = ReadWav(Convert(FakeWwise.Wem(WemFormat.WwisePcm, 2, 44100, samples)));
        Assert.Equal(400, data.Length);
    }

    [Fact]
    public void SixChannelsGetExtensibleHeaderWithSpeakerMask()
    {
        // 5.1: config = mask 0x3F << 12 | type 1 << 8 | 6 channels
        var wav = Convert(FakeWwise.Wem(WemFormat.WwisePcm, 6, 48000, new byte[120], channelConfig: (0x3Fu << 12) | 0x100 | 6));

        var (format, channels, _, _, fmtSize, data) = ReadWav(wav);
        Assert.Equal(0xFFFE, format);
        Assert.Equal(6, channels);
        Assert.Equal(40, fmtSize);
        Assert.Equal(0x3Fu, BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(20 + 20)));
        Assert.Equal(120, data.Length);
    }

    [Fact]
    public void VorbisAndOtherDepthsAreRefused()
    {
        var vorbis = Assert.Throws<InvalidDataException>(() => Convert(FakeWwise.Wem(WemFormat.WwiseVorbis, 1, 48000, new byte[8])));
        Assert.Contains("Wwise Vorbis", vorbis.Message);

        var deep = Assert.Throws<InvalidDataException>(() => Convert(FakeWwise.Wem(WemFormat.WwisePcm, 1, 48000, new byte[9], bits: 24)));
        Assert.Contains("24 bits", deep.Message);
    }
}

public class WemFormatTests
{
    [Fact]
    public void ReadsTheFmtTag()
    {
        Assert.Equal(WemFormat.WwisePcm, WemFormat.ReadFormatTag(FakeWwise.Wem(WemFormat.WwisePcm, 1, 48000, new byte[2])));
        Assert.Equal(WemFormat.WwiseVorbis, WemFormat.ReadFormatTag(FakeWwise.Wem(WemFormat.WwiseVorbis, 1, 48000, new byte[3])));
    }

    [Fact]
    public void GarbageHasNoTag()
    {
        Assert.Null(WemFormat.ReadFormatTag("not a wave file"u8));
        Assert.Null(WemFormat.ReadFormatTag("RIFF\0\0\0\0WAVE"u8));
        Assert.Equal("not a RIFF/WAVE file", WemFormat.Describe(null));
        Assert.Equal("unsupported codec 0x0002", WemFormat.Describe(2));
    }
}

public class SoundbankJsonTests
{
    private const string PerBank = """
        {"SoundBanksInfo": {"SoundBanks": [{
          "Id": "100", "ShortName": "Starfield_WPN",
          "IncludedEvents": [
            {"Id": "7", "Name": "WPN_Fire", "ReferencedStreamedFiles": [
              {"Id": "2", "Language": "SFX", "ShortName": "WPN\\Hand\\Gun\\WPN_Gun_Fire_01.wav"}]},
            {"Id": "8", "Name": "WPN_Reload"}],
          "ReferencedStreamedFiles": [{"Id": "2"}, {"Id": "3", "ShortName": "WPN\\WPN_Click.wav"}],
          "IncludedMemoryFiles": [{"Id": "9", "ShortName": "UI\\Beep.wav"}]
        }]}}
        """;

    [Fact]
    public void ReadsBanksEventsAndFilesWithStringIds()
    {
        var parsed = SoundbankJsonFile.Parse(FakeWwise.Json(PerBank, bom: true));

        var bank = Assert.Single(parsed.Banks);
        Assert.Equal(100u, bank.Id);
        Assert.Equal("Starfield_WPN", bank.Name);
        Assert.Equal(["WPN_Fire", "WPN_Reload"], bank.Events.Select(e => e.Name));
        Assert.Equal([2u], bank.Events[0].SoundIds);
        Assert.Empty(bank.Events[1].SoundIds);
        Assert.Contains(bank.Files, f => f is { Id: 9, InMemory: true });
        // the bank lists sound 2 by ID only; the event's entry supplies its name
        Assert.Contains(bank.Files, f => f.Id == 2 && f.ShortName == "WPN\\Hand\\Gun\\WPN_Gun_Fire_01.wav");
        Assert.Empty(parsed.StreamedFiles);
    }

    [Fact]
    public void NotWwiseJsonIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => SoundbankJsonFile.Parse(FakeWwise.Json("""{"Plugins": []}""")));
    }
}

public class SoundIndexTests
{
    private static string Combined(string name1, string name2) => $$$"""
        {"SoundBanksInfo": {
          "StreamedFiles": [
            {"Id": "1", "ShortName": "{{{name1.Replace("\\", "\\\\")}}}"},
            {"Id": "2", "ShortName": "{{{name2.Replace("\\", "\\\\")}}}"}],
          "SoundBanks": [{"Id": "200", "ShortName": "Starfield_AMB", "ReferencedStreamedFiles": [{"Id": "1"}]}]
        }}
        """;

    private const string PatchBank = """
        {"SoundBanksInfo": {"SoundBanks": [{"Id": "100", "ShortName": "Starfield_WPN",
          "IncludedEvents": [{"Id": "7", "Name": "WPN_Fire", "ReferencedStreamedFiles": [{"Id": "2"}]},
                             {"Id": "8", "Name": "AAA_Fire_Alt", "ReferencedStreamedFiles": [{"Id": "2"}]}],
          "ReferencedStreamedFiles": [{"Id": "2", "ShortName": "WPN\\Hand\\Gun\\WPN_Gun_Fire_02.wav"},
                                      {"Id": "5", "ShortName": "WPN\\Missing.wav"}]}]}}
        """;

    private const string ModBank = """
        {"SoundBanksInfo": {"SoundBanks": [{"Id": "300", "ShortName": "MyMod",
          "ReferencedStreamedFiles": [{"Id": "3", "ShortName": "MyMod_Laser.wav"}]}]}}
        """;

    private static byte[] Wem() => FakeWwise.Wem(WemFormat.WwisePcm, 1, 48000, new byte[4]);

    private static List<SoundSource> Sources() =>
    [
        new("Starfield - WwiseSounds01.ba2", true,
        [
            new MemoryFile("sound\\soundbanks\\1.wem", Wem()),
            new MemoryFile("sound\\soundbanks\\2.wem", Wem()),
            new MemoryFile("sound\\soundbanks\\soundbanksinfo.json", FakeWwise.Json(Combined("AMB\\Wind\\AMB_Wind_01.wav", "WPN\\Old.wav"))),
        ]),
        new("Starfield - WwiseSoundsPatch.ba2", true,
        [
            new MemoryFile("sound/soundbanks/2.wem", Wem()),
            new MemoryFile("sound/soundbanks/100.json", FakeWwise.Json(PatchBank, bom: true)),
            new MemoryFile("sound/soundbanks/plugininfo.json", FakeWwise.Json("""{"PluginInfo": {}}""")),
        ]),
        new("MyMod - Main.ba2", false,
        [
            new MemoryFile("sound/soundbanks/3.wem", Wem()),
            new MemoryFile("sound/soundbanks/300.json", FakeWwise.Json(ModBank)),
            // a leftover full copy that renames a vanilla sound: must not win over the game's copy
            new MemoryFile("sound/soundbanks/soundbanksinfo.json", FakeWwise.Json(Combined("AMB\\Hacked.wav", "WPN\\Hacked.wav"))),
            new MemoryFile("sound/soundbanks/broken.json", FakeWwise.Json("{")),
        ]),
        new(SoundSource.LooseName, false, [new MemoryFile("sound/soundbanks/4.wem", Wem())]),
    ];

    [Fact]
    public void ListsEverySoundWithTheWinningSourceAndItsName()
    {
        var result = SoundIndex.Build(Sources());
        var byId = result.Sounds.ToDictionary(s => s.Id);

        Assert.Equal([1u, 2u, 3u, 4u], byId.Keys.Order());
        Assert.Equal(4, result.SourcesWithSounds);

        var wind = byId[1];
        Assert.Equal("AMB_Wind_01", wind.Name);
        Assert.Equal("AMB", wind.Group);
        Assert.Equal("Wind", wind.Folder);
        Assert.Equal("Starfield_AMB", wind.Bank);
        Assert.False(wind.FromMod);

        var gun = byId[2];
        Assert.Equal("Starfield - WwiseSoundsPatch.ba2", gun.Source);
        Assert.Equal(["Starfield - WwiseSounds01.ba2"], gun.Replaces);
        Assert.Equal("WPN_Gun_Fire_02", gun.Name); // the bank's own file beats the combined file
        Assert.Equal("Hand\\Gun", gun.Folder);
        Assert.Equal(["AAA_Fire_Alt", "WPN_Fire"], gun.Events);
        Assert.Equal("sound/soundbanks/2.wem", gun.FilePath);

        var laser = byId[3];
        Assert.True(laser.FromMod);
        Assert.Equal("MyMod_Laser", laser.Name);
        Assert.Equal("Other", laser.Group);
        Assert.Equal("MyMod", laser.Bank);

        var loose = byId[4];
        Assert.Equal(SoundSource.LooseName, loose.Source);
        Assert.Equal("4", loose.Name);
        Assert.Equal(1, result.Unnamed);
        Assert.Equal(1, result.NamedButMissing); // sound 5
        Assert.Equal(4, result.Files.Count);
        Assert.Equal("sound/soundbanks/2.wem", result.Files[2].Path); // the Patch archive's file
    }

    [Fact]
    public void ModCopyOfCombinedFileOnlyFillsGaps()
    {
        var sources = Sources();
        // without the game's combined file, the mod's copy is all there is for sound 1
        sources[0] = new SoundSource(sources[0].Name, true, sources[0].Files.Where(f => !f.Path.EndsWith(".json")).ToList());

        var wind = SoundIndex.Build(sources).Sounds.Single(s => s.Id == 1);
        Assert.Equal("Hacked", wind.Name);
    }

    [Theory]
    [InlineData("sound/soundbanks/123.wem", true)]
    [InlineData("Sound\\Soundbanks\\123.json", true)]
    [InlineData("sound/soundbanks/123.bnk", false)]
    [InlineData("sound/soundbanks/sub/123.wem", false)]
    [InlineData("sound/voice/starfield.esm/npcfsarah/1.wem", false)]
    public void OnlyFilesDirectlyInSoundbanksCount(string path, bool expected) =>
        Assert.Equal(expected, SoundIndex.IsSoundbankFile(path));

    [Fact]
    public void BaseGameArchivesAreRecognised()
    {
        Assert.True(SoundIndex.IsBaseGameArchive("Starfield - WwiseSounds01.ba2"));
        Assert.False(SoundIndex.IsBaseGameArchive("ShatteredSpace - Main01.ba2"));
    }

    /// <summary>
    /// Set STARFIELDVT_TEST_DATA_DIR to a Starfield Data folder to index the real game (read only);
    /// without it the test passes without doing anything. The summary goes to the test output.
    /// </summary>
    [Fact]
    public void RealGameIndexHasNamedSounds()
    {
        var data = Environment.GetEnvironmentVariable("STARFIELDVT_TEST_DATA_DIR");
        if (string.IsNullOrEmpty(data) || !Directory.Exists(data)) return;

        var result = SoundIndex.Build(data);
        var named = result.Sounds.Count(s => s.ShortName != null);
        var summary = $"sounds={result.Sounds.Count} named={named} mods={result.Sounds.Count(s => s.FromMod)} " +
                      $"replacing={result.Sounds.Count(s => s.Replaces.Count > 0)} withEvents={result.Sounds.Count(s => s.Events.Count > 0)} " +
                      $"unnamed={result.Unnamed} missing={result.NamedButMissing} sources={result.SourcesWithSounds} " +
                      $"groups={string.Join(",", result.Sounds.GroupBy(s => s.Group).OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key}:{g.Count()}"))}";
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "svt-real-index.txt"), summary);

        Assert.True(result.Sounds.Count > 1000, summary);
        Assert.True(named > result.Sounds.Count / 2, summary);
        Assert.All(result.Sounds, s => Assert.True(result.Files.ContainsKey(s.Id)));
    }

    [Fact]
    public void LooseFilesComeLastAndWin()
    {
        var dir = Directory.CreateTempSubdirectory("svt-sounds-");
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(dir.FullName, "Sound", "SoundBanks"));
            File.WriteAllBytes(Path.Combine(folder.FullName, "1.wem"), Wem());
            File.WriteAllText(Path.Combine(folder.FullName, "__folder_managed_by_vortex"), "");

            var sources = SoundIndex.ReadSources(dir.FullName); // no archives in this folder
            var loose = Assert.Single(sources);
            Assert.Equal(SoundSource.LooseName, loose.Name);
            Assert.Single(loose.Files);

            var result = SoundIndex.Build(Sources().Take(1).Concat(sources));
            var wind = result.Sounds.Single(s => s.Id == 1);
            Assert.Equal(SoundSource.LooseName, wind.Source);
            Assert.True(wind.FromMod);
            Assert.Equal("AMB_Wind_01", wind.Name);
        }
        finally
        {
            dir.Delete(true);
        }
    }
}

public class SoundCatalogTests
{
    private static SoundEffect Sound(uint id, string name, string group, bool fromMod = false, string source = "Starfield - WwiseSounds01.ba2",
        string folder = "", string? bank = null, params string[] events) => new()
    {
        Id = id, Name = name, Group = group, FromMod = fromMod, Source = source, Folder = folder, Bank = bank, Events = [.. events],
    };

    private static readonly List<SoundEffect> Sounds =
    [
        Sound(1, "AMB_Wind_01", "AMB", folder: "Exteriors\\Wind"),
        Sound(2, "WPN_Gun_Fire_02", "WPN", bank: "Starfield_WPN", events: ["WPN_Fire", "WPN_Fire_Alt"]),
        Sound(3, "WPN_Lightsaber_Swing", "WPN", fromMod: true, source: "Sabers - Main.ba2"),
        Sound(4, "SFBGS001_Vortex", "Creations", fromMod: true, source: "ShatteredSpace - Main01.ba2"),
        Sound(80667371, "MUS_AstralLounge_A", "MUS", events: ["MUS_AstralLounge"]),
    ];

    [Fact]
    public void GroupsAreAllThenGameGroupsThenMods()
    {
        var groups = SoundCatalog.Groups(Sounds);

        Assert.Equal(["All sounds", "AMB (1)", "MUS (1)", "WPN (1)", "Mod: Sabers - Main.ba2 (1)", "Mod: ShatteredSpace - Main01.ba2 (1)"],
            groups.Select(g => g.Label));
        Assert.Equal(5, groups[0].Count);
        // a mod's sound in a vanilla folder is listed under its mod, not under the game's group
        Assert.Equal([2u], Sounds.Where(groups[3].Contains).Select(s => s.Id));
        Assert.Equal([3u], Sounds.Where(groups[4].Contains).Select(s => s.Id));
        Assert.All(Sounds, s => Assert.True(groups[0].Contains(s)));
    }

    [Theory]
    [InlineData("", new uint[] { 1, 2, 3, 4, 80667371 })]
    [InlineData("lightsaber", new uint[] { 3 })]
    [InlineData("80667371", new uint[] { 80667371 })]
    [InlineData("astrallounge", new uint[] { 80667371 })]
    [InlineData("wpn_fire_alt", new uint[] { 2 })]
    [InlineData("wind exteriors", new uint[] { 1 })]
    [InlineData("wind gun", new uint[] { })]
    [InlineData("shatteredspace", new uint[] { 4 })]
    [InlineData("starfield_wpn", new uint[] { 2 })]
    [InlineData("8066", new uint[] { })] // part of an ID is not a match: IDs are matched whole
    public void SearchMatchesEveryWord(string search, uint[] expected) =>
        Assert.Equal(expected, Sounds.Where(s => SoundCatalog.Matches(s, SoundCatalog.Terms(search))).Select(s => s.Id));

    [Fact]
    public void ExportNamesAreSafe()
    {
        Assert.Equal("WPN_Gun_Fire_02_2.wav", SoundCatalog.ExportFileName(Sounds[1]));
        Assert.Equal("a_b_c__5.wav", SoundCatalog.ExportFileName(Sound(5, "a:b/c?", "Other")));
        Assert.Equal("6.wav", SoundCatalog.ExportFileName(Sound(6, "6", "Other")));
        Assert.Equal("7.wav", SoundCatalog.ExportFileName(Sound(7, " .. ", "Other")));
    }
}
