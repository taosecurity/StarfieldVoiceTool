using StarfieldVT.Core;

namespace StarfieldVT.Core.Tests;

public class VoicePathsTests
{
    [Theory]
    [InlineData("Starfield - Voices01")]
    [InlineData("Starfield - Voices02")]
    [InlineData("Starfield - VoicesPatch")]
    [InlineData("SFBGS003 - Voices_en")]
    [InlineData("sfta01 - voices_en")]
    [InlineData("MyMod - Voices")]
    public void IsEnglishVoiceArchive_AcceptsEnglishAndNeutralArchives(string name)
    {
        Assert.True(VoicePaths.IsEnglishVoiceArchive(name));
    }

    [Theory]
    [InlineData("SFBGS003 - Voices_de")]
    [InlineData("GB_SBCX_ShatteredSpace - Voices_fr")]
    [InlineData("GB_ShipBuilderConfigurator_free - Voices_fr_ps")]
    [InlineData("sfta01 - voices_ja")]
    [InlineData("Starfield - Textures01")]
    [InlineData("ShatteredSpace - Main01")]
    public void IsEnglishVoiceArchive_RejectsOtherLanguagesAndNonVoiceArchives(string name)
    {
        Assert.False(VoicePaths.IsEnglishVoiceArchive(name));
    }

    [Theory]
    [InlineData("sound/voice/starfield.esm/npcmboe/00123456.wem", "starfield.esm")]
    [InlineData(@"sound\voice\MyMod.esp\npcfx\00abcdef.wem", "MyMod.esp")]
    [InlineData("sound/voice/LightMod.esl/npcfx/00abcdef.wem", "LightMod.esl")]
    public void GetPluginFolder_FindsPluginSegment(string path, string expected)
    {
        Assert.Equal(expected, VoicePaths.GetPluginFolder(path));
    }

    [Fact]
    public void GetPluginFolder_ReturnsNullWhenNoPluginSegment()
    {
        Assert.Null(VoicePaths.GetPluginFolder("sound/voice/shared/00123456.wem"));
    }

    [Theory]
    [InlineData("sound/voice/starfield.esm/npcmboe/00123456.wem", "npcmboe")]
    [InlineData(@"sound\voice\starfield.esm\NPCFAndy\00123456.wem", "NPCFAndy")]
    public void GetVoiceType_IsContainingFolder(string path, string expected)
    {
        Assert.Equal(expected, VoicePaths.GetVoiceType(path));
    }

    [Fact]
    public void GetVoiceType_ReturnsNullForBareFileName()
    {
        Assert.Null(VoicePaths.GetVoiceType("00123456.wem"));
    }

    [Theory]
    [InlineData(0x0512AB34u, "0012ab34.wem")]
    [InlineData(0x00000001u, "00000001.wem")]
    [InlineData(0xFF00000Au, "0000000a.wem")]
    public void GetArchiveWemFileName_ZeroesLoadOrderByte(uint wemFile, string expected)
    {
        Assert.Equal(expected, VoicePaths.GetArchiveWemFileName(wemFile));
    }

    [Fact]
    public void ArchivePathsEqual_IgnoresCaseAndSeparators()
    {
        Assert.True(VoicePaths.ArchivePathsEqual(@"Sound\Voice\Starfield.esm\X\00000001.wem",
            "sound/voice/starfield.esm/x/00000001.wem"));
        Assert.False(VoicePaths.ArchivePathsEqual("sound/voice/a.esm/x/1.wem", "sound/voice/a.esm/y/1.wem"));
    }

    [Fact]
    public void WemKey_IsCaseInsensitive()
    {
        Assert.Equal(new WemKey("Starfield.esm", "0012AB34.wem"), new WemKey("starfield.esm", "0012ab34.wem"));
        Assert.Equal(new WemKey("Starfield.esm", "0012AB34.wem").GetHashCode(),
            new WemKey("starfield.esm", "0012ab34.wem").GetHashCode());
        Assert.NotEqual(new WemKey("a.esm", "1.wem"), new WemKey("b.esm", "1.wem"));
    }
}
