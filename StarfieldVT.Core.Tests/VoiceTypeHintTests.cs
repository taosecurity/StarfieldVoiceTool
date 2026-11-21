using StarfieldVT.Core.Export;
using StarfieldVT.Core.Models;

namespace StarfieldVT.Core.Tests;

public class VoiceTypeHintTests
{
    private static VoiceType Vt(string master, string id) =>
        new() { FromMaster = master, EditorId = id, VoiceLines = Array.Empty<VoiceLine>() };

    private static readonly List<Master> Tree =
    [
        new("Starfield.esm", [Vt("Starfield.esm", "npcfsarahmorgan"), Vt("Starfield.esm", "npcmsamcoe")]),
        new("AstridCrew - Romance.esm", [Vt("AstridCrew - Romance.esm", "npcfastrid")]),
        new("Other.esm", [Vt("Other.esm", "NPCFAstrid")]),
    ];

    [Fact]
    public void VoiceTypeOnlyInOtherPluginsNamesThem()
    {
        var hint = VoiceTypeHint.For(Tree, "Starfield.esm", "NPCFAstrid");
        Assert.Contains("AstridCrew - Romance.esm", hint);
        Assert.Contains("Other.esm", hint);
        Assert.DoesNotContain("Starfield.esm", hint);
        Assert.Contains("--plugin", hint);
    }

    [Fact]
    public void NearNamesInThePickedPlugin()
    {
        var hint = VoiceTypeHint.For(Tree, "starfield.esm", "sarah");
        Assert.Equal("Did you mean: npcfsarahmorgan", hint);
    }

    [Fact]
    public void NearNamesWithoutAPluginSearchEverything()
    {
        var hint = VoiceTypeHint.For(Tree, null, "astr");
        Assert.Equal("Did you mean: npcfastrid", hint); // case-insensitive duplicates shown once
    }

    [Fact]
    public void NothingCloseSaysHowToList()
    {
        var hint = VoiceTypeHint.For(Tree, null, "zzz");
        Assert.Contains("--list", hint);
    }
}
