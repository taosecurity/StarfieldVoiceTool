using StarfieldVT.Core.Models;

namespace StarfieldVT.Core.Tests;

public class SearchScopeTests
{
    private static VoiceLine Line(string voiceType, string id, string dialogue) =>
        new($"sound/voice/kinggathcreations_spaceship.esm/{voiceType}/{id}.wem", dialogue,
            "kinggathcreations_spaceship.esm", voiceType);

    [Fact]
    public void SearchStaysInsideTheVoiceTypeFilter()
    {
        // the reported case: plugin selected from a tree filtered to "sarahmorgan", then a search for "weapons"
        var shown = new[] { Line("npcfsarahmorgan", "0069d86e", "Weapons are in the green!"), Line("npcfsarahmorgan", "00000001", "Hello.") };
        var searchHits = new[]
        {
            Line("npcfsarahmorgan", "0069d86e", "Weapons are in the green!"),
            Line("kgcShip_VTYP_Torin", "00b2a185", "Weapons repaired!"),
            Line("npcmbarrett", "002bf32e", "Weapons ready, please and thanks."),
        };

        var result = SearchScope.Of(shown).Restrict(searchHits).ToList();

        Assert.Single(result);
        Assert.Equal("npcfsarahmorgan", result[0].VoiceType);
    }

    [Fact]
    public void MatchingIgnoresCase()
    {
        var shown = new[] { Line("npcfsarahmorgan", "0069D86E", "x") };
        var hit = Line("NPCFSarahMorgan", "0069d86e", "x");
        hit.Filename = hit.Filename.ToUpperInvariant();

        Assert.Single(SearchScope.Of(shown).Restrict(new[] { hit }));
    }

    [Fact]
    public void EmptyTableSearchesEverything()
    {
        var hits = new[] { Line("a", "1", "x"), Line("b", "2", "y") };
        Assert.True(SearchScope.Everything.IsEverything);
        Assert.Equal(2, SearchScope.Everything.Restrict(hits).Count());
    }

    [Fact]
    public void ASelectionWithNoLinesFindsNothing()
    {
        // e.g. a voice type filter that matches nothing: searching must not fall back to everything
        Assert.Empty(SearchScope.Of(Array.Empty<VoiceLine>()).Restrict(new[] { Line("a", "1", "x") }));
    }
}
