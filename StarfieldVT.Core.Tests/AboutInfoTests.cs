using StarfieldVT.Core.Filesystem;

namespace StarfieldVT.Core.Tests;

public class AboutInfoTests
{
    [Fact]
    public void VersionHasThreeParts()
    {
        var version = AboutInfo.Version(typeof(AboutInfo).Assembly);
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }

    [Fact]
    public void DetailsListWhatABugReportNeeds()
    {
        var details = AboutInfo.Details("0.8.1", @"C:\Games\Starfield\Data", includeAllModVoices: true);

        Assert.Contains("Starfield Voice Tool 0.8.1", details);
        Assert.Contains(@"Data folder: C:\Games\Starfield\Data", details);
        Assert.Contains("all mod archives: on", details);
        Assert.Contains(AppDataFolder.GetLogDir(), details);
        Assert.Contains("OS: ", details);
    }

    [Fact]
    public void DetailsSayWhenNoDataFolderIsSet()
    {
        Assert.Contains("Data folder: (not set)", AboutInfo.Details("0.8.1", null, includeAllModVoices: false));
    }

    [Fact]
    public void LinksPointAtTheFork()
    {
        Assert.StartsWith("https://github.com/taosecurity/StarfieldVoiceTool", AboutInfo.IssuesUrl);
        Assert.EndsWith("/releases/latest", AboutInfo.LatestReleaseUrl);
    }
}
