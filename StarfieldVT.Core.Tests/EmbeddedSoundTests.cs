using StarfieldVT.Core.Sounds;

namespace StarfieldVT.Core.Tests;

public class EmbeddedSoundTests
{
    /// <summary>A Wwise bank: BKHD, DIDX (id, offset in DATA, size) and DATA, items 16-byte aligned like Wwise.</summary>
    private static byte[] Bank(params (uint Id, byte[] Bytes)[] items)
    {
        using var data = new MemoryStream();
        var index = new List<(uint, uint, uint)>();
        foreach (var (id, bytes) in items)
        {
            while (data.Length % 16 != 0) data.WriteByte(0);
            index.Add((id, (uint)data.Length, (uint)bytes.Length));
            data.Write(bytes);
        }

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("BKHD"u8);
        w.Write(8);
        w.Write(140);
        w.Write(1234);
        w.Write("DIDX"u8);
        w.Write(index.Count * 12);
        foreach (var (id, offset, size) in index)
        {
            w.Write(id);
            w.Write(offset);
            w.Write(size);
        }

        w.Write("DATA"u8);
        w.Write((int)data.Length);
        w.Write(data.ToArray());
        w.Write("HIRC"u8);
        w.Write(4);
        w.Write(0);
        w.Flush();
        return ms.ToArray();
    }

    private static readonly byte[] Breath = FakeWwise.Wem(WemFormat.WwisePcm, 1, 48000, [1, 2, 3, 4, 5, 6]);
    private static readonly byte[] Prefetch = FakeWwise.Wem(WemFormat.WwiseVorbis, 1, 48000, new byte[10]);
    private static readonly byte[] ReverbData = [0x00, 0x04, 0x02, 0x00, 1, 2, 3, 4];
    private static readonly byte[] Unknown = [0x87, 0xD4, 0x0E, 0x3F, 9, 9];

    private const string BankJson = """
        {"SoundBanksInfo": {"SoundBanks": [{"Id": "500", "ShortName": "Starfield_VOC",
          "IncludedEvents": [{"Id": "9", "Name": "VOC_Player_Sprint", "ReferencedStreamedFiles": [{"Id": "10"}]}],
          "IncludedMemoryFiles": [
            {"Id": "10", "ShortName": "VOC\\Player\\Male\\VOC_Player_M_Breath_01.wav", "Path": "SFX\\VOC\\Player\\Male\\VOC_Player_M_Breath_01_1234.wem"},
            {"Id": "11", "ShortName": "AK_Factory_IR\\Hall.wav", "Path": "Plugins\\Wwise Convolution Reverb\\AK_Factory_IR\\Hall_ABCD.wem"},
            {"Id": "2", "ShortName": "WPN\\Gun.wav", "Path": "SFX\\WPN\\Gun_1.wem", "PrefetchSize": "8"}]}]}}
        """;

    private static List<SoundSource> Sources(byte[] bank) =>
    [
        new("Starfield - WwiseSounds01.ba2", true,
        [
            new MemoryFile("sound/soundbanks/2.wem", Prefetch),
            new MemoryFile("sound/soundbanks/500.bnk", bank),
            new MemoryFile("sound/soundbanks/500.json", FakeWwise.Json(BankJson)),
        ]),
    ];

    [Fact]
    public void ReadsTheBankIndex()
    {
        var bank = Bank((10, Breath), (2, Prefetch[..8]));
        var entries = BnkMedia.Read(bank);

        Assert.Equal([10u, 2u], entries.Select(e => e.Id));
        Assert.Equal(Breath, bank.AsSpan(entries[0].Offset, entries[0].Length).ToArray());
        Assert.Equal(0, (entries[1].Offset - entries[0].Offset) % 16); // offsets are relative to DATA, which we add
    }

    [Fact]
    public void BanksWithoutMediaOrTruncatedGiveNothing()
    {
        Assert.Empty(BnkMedia.Read("BKHD\u0008\0\0\0\0\0\0\0\0\0\0\0"u8));
        var bank = Bank((10, Breath));
        Assert.Empty(BnkMedia.Read(bank.AsSpan(0, bank.Length - 40))); // DATA cut short
        Assert.Empty(BnkMedia.Read([]));
    }

    [Fact]
    public void ListsOnlyRealSoundsFoundOnlyInBanks()
    {
        var result = SoundIndex.Build(Sources(Bank((10, Breath), (2, Prefetch[..8]), (11, ReverbData), (12, Unknown))));

        // 2 is listed once, as its streamed .wem; 11 is reverb data; 12 isn't a sound format we know
        Assert.Equal([2u, 10u], result.Sounds.Select(s => s.Id).Order());
        var breath = result.Sounds.Single(s => s.Id == 10);
        Assert.Equal("VOC_Player_M_Breath_01", breath.Name);
        Assert.Equal("VOC", breath.Group);
        Assert.Equal("Player\\Male", breath.Folder);
        Assert.Equal("Starfield_VOC", breath.Bank);
        Assert.Equal("500.bnk", breath.EmbeddedIn);
        Assert.Equal("Starfield - WwiseSounds01.ba2, inside 500.bnk", breath.SourceText);
        Assert.Equal(["VOC_Player_Sprint"], breath.Events);
        Assert.False(breath.FromMod);

        Assert.Null(result.Sounds.Single(s => s.Id == 2).EmbeddedIn);
        Assert.Equal("Starfield - WwiseSounds01.ba2", result.Sounds.Single(s => s.Id == 2).SourceText);

        // playing or exporting reads exactly the embedded bytes, which convert like any PCM sound
        var bytes = result.Files[10].GetBytes();
        Assert.Equal(Breath, bytes);
        using var wav = new MemoryStream();
        WwisePcm.ToWav(bytes, wav);
        Assert.True(wav.Length > 44);
    }

    [Fact]
    public void TheLaterCopyOfABankWins()
    {
        var sources = Sources(Bank((10, Breath)));
        var newer = FakeWwise.Wem(WemFormat.WwiseVorbis, 2, 44100, new byte[20]);
        sources.Add(new SoundSource("MyMod - Main.ba2", false, [new MemoryFile("sound/soundbanks/500.bnk", Bank((13, newer)))]));

        var result = SoundIndex.Build(sources);

        Assert.DoesNotContain(result.Sounds, s => s.Id == 10);
        var mod = result.Sounds.Single(s => s.Id == 13);
        Assert.True(mod.FromMod);
        Assert.Equal("MyMod - Main.ba2", mod.Source);
        Assert.Equal("13", mod.Name); // the mod shipped no JSON naming it
        Assert.Equal(newer, result.Files[13].GetBytes());
    }

    [Fact]
    public void PluginDataIsRecognisedFromItsPath()
    {
        Assert.True(new SoundFileInfo(1, "x", true, "Plugins\\iZotope Hybrid Reverb\\x.wem").IsPluginData);
        Assert.False(new SoundFileInfo(1, "x", true, "SFX\\VOC\\x.wem").IsPluginData);
        Assert.False(new SoundFileInfo(1, "x", true).IsPluginData);
    }
}
