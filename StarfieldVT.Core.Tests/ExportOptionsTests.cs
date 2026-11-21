using StarfieldVT.Core.Export;

namespace StarfieldVT.Core.Tests;

public class ExportOptionsTests
{
    private static readonly string Out = Path.Combine(Path.GetTempPath(), "svt-export");

    private static ExportOptions Parse(params string[] args) => ExportOptions.Parse(args);

    [Fact]
    public void ExitCodesMatchThePlan()
    {
        Assert.Equal(0, ExportOptions.ExitOk);
        Assert.Equal(1, ExportOptions.ExitSomeFailed);
        Assert.Equal(2, ExportOptions.ExitBadOptions);
        Assert.Equal(3, ExportOptions.ExitNoGame);
        Assert.Equal(4, ExportOptions.ExitFailed);
        Assert.Equal(130, ExportOptions.ExitCancelled);
        Assert.Contains("4 the export couldn't run", ExportOptions.Usage);
    }

    [Theory]
    [InlineData(true, false, true, VoiceIndexAction.None)]      // cache and no search: window may stay open
    [InlineData(true, false, false, VoiceIndexAction.None)]
    [InlineData(true, true, true, VoiceIndexAction.StopWindowOpen)]
    [InlineData(false, false, true, VoiceIndexAction.StopWindowOpen)] // a rebuild would delete the window's index
    [InlineData(false, false, false, VoiceIndexAction.Rebuild)]
    [InlineData(false, true, false, VoiceIndexAction.Rebuild)]
    [InlineData(true, true, false, VoiceIndexAction.Search)]
    public void VoiceIndexPlanChooses(bool cacheCurrent, bool search, bool windowRunning, VoiceIndexAction expected)
    {
        var asked = false;
        var action = VoiceIndexPlan.Choose(cacheCurrent, search, windowRunning, () =>
        {
            asked = true;
            return 10;
        });
        Assert.Equal(expected, action);
        Assert.Equal(expected == VoiceIndexAction.Search, asked); // the index is opened only when it will be searched
    }

    [Fact]
    public void OnlyThisAccountsWindowsCount()
    {
        Assert.True(VoiceIndexPlan.AnyOfMine([new(true, false)])); // my window, another session (SSH, a Linux terminal)
        Assert.False(VoiceIndexPlan.AnyOfMine([new(false, true)])); // another account's window
        Assert.False(VoiceIndexPlan.AnyOfMine([new(false, false), new(null, false)])); // owner unknown: the session decides
        Assert.True(VoiceIndexPlan.AnyOfMine([new(null, true)]));
        Assert.True(VoiceIndexPlan.AnyOfMine([new(null, null)])); // nothing readable: count it
        Assert.False(VoiceIndexPlan.AnyOfMine([]));
    }

    [Fact]
    public void ALuceneLockIsRecognized()
    {
        var locked = new Lucene.Net.Store.LockObtainFailedException("Lock obtain timed out");
        Assert.True(VoiceIndexPlan.IsIndexLocked(locked));
        Assert.True(VoiceIndexPlan.IsIndexLocked(new InvalidOperationException("outer", locked)));
        Assert.False(VoiceIndexPlan.IsIndexLocked(new IOException("disk full")));
        Assert.False(VoiceIndexPlan.IsIndexLocked(null));
        Assert.Contains("Close the window", VoiceIndexPlan.IndexLockedMessage);
    }

    [Fact]
    public void AnEmptyIndexIsRefilledBeforeSearching() =>
        Assert.Equal(VoiceIndexAction.Refill, VoiceIndexPlan.Choose(true, true, false, () => 0));

    [Fact]
    public void SoundsWithOutHasDefaults()
    {
        var o = Parse("sounds", "--out", Out);
        Assert.Empty(o.Errors);
        Assert.Equal(0, o.ExitCode);
        Assert.Equal(ExportKind.Sounds, o.Kind);
        Assert.Equal(Path.GetFullPath(Out), o.OutFolder);
        Assert.Null(o.Group);
        Assert.Null(o.Search);
        Assert.Null(o.GameFolder);
        Assert.Null(o.LogFile);
        Assert.False(o.Flat);
        Assert.False(o.NoCsv);
        Assert.False(o.List);
        Assert.False(o.Verbose);
        Assert.False(o.Help);
    }

    [Fact]
    public void EverySoundOption()
    {
        var log = Path.Combine(Path.GetTempPath(), "svt-export.log");
        var o = Parse("SOUNDS", "--out", Out, "--group", "WPN", "--search", "lightsaber hum", "--flat", "--no-csv",
            "--list", "--game", @"C:\Games\Starfield\Data", "--verbose", "--log", log);
        Assert.Empty(o.Errors);
        Assert.Equal(ExportKind.Sounds, o.Kind);
        Assert.Equal("WPN", o.Group);
        Assert.Equal("lightsaber hum", o.Search);
        Assert.True(o.Flat);
        Assert.True(o.NoCsv);
        Assert.True(o.List);
        Assert.Equal(@"C:\Games\Starfield\Data", o.GameFolder);
        Assert.True(o.Verbose);
        Assert.Equal(log, o.LogFile);
    }

    [Fact]
    public void EveryVoiceOption()
    {
        var o = Parse("voices", "--out", Out, "--plugin", "Starfield.esm", "--voice-type", "NPCFAstrid",
            "--search", "good to see you", "-v");
        Assert.Empty(o.Errors);
        Assert.Equal(ExportKind.Voices, o.Kind);
        Assert.Equal("Starfield.esm", o.Plugin);
        Assert.Equal("NPCFAstrid", o.VoiceType);
        Assert.Equal("good to see you", o.Search);
        Assert.True(o.Verbose);
    }

    [Fact]
    public void ValuesAfterEquals()
    {
        var o = Parse("voices", "--out=" + Out, "--plugin=Starfield.esm", "--voice-type=NPCFAstrid", "--search=a=b");
        Assert.Empty(o.Errors);
        Assert.Equal(Path.GetFullPath(Out), o.OutFolder);
        Assert.Equal("Starfield.esm", o.Plugin);
        Assert.Equal("NPCFAstrid", o.VoiceType);
        Assert.Equal("a=b", o.Search);
    }

    [Fact]
    public void QuotedPathsAreCleanedUp()
    {
        var o = Parse("sounds", "--out", "\"" + Out + "\"");
        Assert.Empty(o.Errors);
        Assert.Equal(Path.GetFullPath(Out), o.OutFolder);
    }

    [Fact]
    public void ListNeedsNoOut()
    {
        var o = Parse("sounds", "--list");
        Assert.Empty(o.Errors);
        Assert.True(o.List);
        Assert.Null(o.OutFolder);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void HelpNeedsNothingElse(string flag)
    {
        var o = Parse(flag);
        Assert.True(o.Help);
        Assert.Empty(o.Errors);
        Assert.Equal(0, o.ExitCode);
    }

    [Fact]
    public void UsageNamesEveryOption()
    {
        foreach (var option in new[] { "--out", "--group", "--plugin", "--voice-type", "--search", "--flat", "--no-csv",
                     "--list", "--game", "--verbose", "--log", "--help", "130" })
        {
            Assert.Contains(option, ExportOptions.Usage);
        }
    }

    [Fact]
    public void NoArgumentsIsAnError()
    {
        var o = Parse();
        Assert.Equal(2, o.ExitCode);
        Assert.Contains("sounds", o.Errors[0]);
    }

    [Fact]
    public void MissingKindIsAnError()
    {
        var o = Parse("--out", Out);
        Assert.Equal(2, o.ExitCode);
        Assert.Contains("Say what to export first", o.Errors[0]);
    }

    [Fact]
    public void MissingOutIsAnError()
    {
        var o = Parse("sounds");
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.Contains("--out is needed"));
    }

    [Theory]
    [InlineData("--out")]
    [InlineData("--group")]
    [InlineData("--plugin")]
    [InlineData("--voice-type")]
    [InlineData("--search")]
    [InlineData("--game")]
    [InlineData("--log")]
    public void OptionAtTheEndWithoutValueIsAnError(string option)
    {
        var o = Parse("voices", "--list", option);
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.StartsWith(option + " needs a value"));
    }

    [Fact]
    public void OptionFollowedByAnotherOptionHasNoValue()
    {
        var o = Parse("sounds", "--out", "--flat");
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.StartsWith("--out needs a value"));
        Assert.True(o.Flat);
    }

    [Fact]
    public void EmptyValueAfterEqualsIsAnError()
    {
        var o = Parse("sounds", "--list", "--group=");
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.StartsWith("--group= needs a value"));
    }

    [Fact]
    public void FlagWithValueIsAnError()
    {
        var o = Parse("sounds", "--list", "--flat=yes");
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.Contains("takes no value"));
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("-x")]
    [InlineData("extra")]
    public void UnknownOptionIsAnError(string arg)
    {
        var o = Parse("sounds", "--list", arg);
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.Contains("Unknown option") && e.Contains(arg));
    }

    [Fact]
    public void KindNotFirstIsAnError()
    {
        var o = Parse("--list", "sounds");
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.Contains("must come first"));
    }

    [Fact]
    public void KindTwiceIsAnError()
    {
        var o = Parse("sounds", "voices", "--list");
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.Contains("must come first, and only once"));
    }

    [Fact]
    public void VoiceFiltersOnSoundsAreErrors()
    {
        var o = Parse("sounds", "--list", "--plugin", "Starfield.esm", "--voice-type", "NPCFAstrid");
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.StartsWith("--plugin is for voices"));
        Assert.Contains(o.Errors, e => e.StartsWith("--voice-type is for voices"));
    }

    [Fact]
    public void GroupOnVoicesIsAnError()
    {
        var o = Parse("voices", "--list", "--group", "WPN");
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.StartsWith("--group is for sounds"));
    }

    [Fact]
    public void UnusablePathIsAnError()
    {
        var o = Parse("sounds", "--out", "\"\"");
        Assert.Equal(2, o.ExitCode);
        Assert.Contains(o.Errors, e => e.Contains("not a usable path"));
    }
}
