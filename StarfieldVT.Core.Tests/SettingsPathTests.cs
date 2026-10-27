using StarfieldVT.Core;

namespace StarfieldVT.Core.Tests;

public class SettingsPathTests
{
    [Theory]
    [InlineData("  C:\\Games\\Starfield\\Data  ", "C:\\Games\\Starfield\\Data")]
    [InlineData("\"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Starfield\"", "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Starfield")]
    [InlineData("", "")]
    [InlineData("\"\"", "")]
    public void CleanUpTypedPath_TrimsWhitespaceAndQuotes(string typed, string expected)
    {
        Assert.Equal(expected, SettingsManager.CleanUpTypedPath(typed));
    }

    [Fact]
    public void CleanUpTypedPath_ExpandsHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(home + "/games/Data", SettingsManager.CleanUpTypedPath("~/games/Data"));
        Assert.Equal(home, SettingsManager.CleanUpTypedPath("~"));
    }

    [Fact]
    public void NormalizeDataFolder_AcceptsDataOrInstallFolder()
    {
        using var dir = new TempDir();
        var data = Path.Combine(dir.Path, "Data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "Starfield.esm"), "");

        Assert.Equal(data, SettingsManager.NormalizeDataFolder(data));
        Assert.Equal(data, SettingsManager.NormalizeDataFolder(dir.Path));
        Assert.Equal(data, SettingsManager.NormalizeDataFolder($"  \"{dir.Path}\" "));
    }

    [Fact]
    public void NormalizeDataFolder_RejectsFoldersWithoutStarfieldEsm()
    {
        using var dir = new TempDir();

        Assert.Null(SettingsManager.NormalizeDataFolder(dir.Path));
        Assert.Null(SettingsManager.NormalizeDataFolder(Path.Combine(dir.Path, "missing")));
        Assert.Null(SettingsManager.NormalizeDataFolder("   "));
    }
}
