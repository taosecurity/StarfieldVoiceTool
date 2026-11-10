using System.Text.Json;

namespace StarfieldVT.Core.Tests;

public class SettingsUpgradeTests
{
    [Fact]
    public void NewSettingsIncludeAllModVoices()
    {
        Assert.True(new AppSettings().IncludeAllModVoices);
    }

    [Fact]
    public void OldSettingsFileIsSwitchedOnOnce()
    {
        // what 0.7.0 wrote: the option saved as false, no version
        var old = JsonSerializer.Deserialize<AppSettings>(
            "{\"StarfieldDataPath\":\"C:\\\\Games\\\\Starfield\\\\Data\",\"IncludeAllModVoices\":false}")!;

        Assert.True(old.Upgrade());
        Assert.True(old.IncludeAllModVoices);
        Assert.Equal(AppSettings.CurrentVersion, old.SettingsVersion);
        Assert.Equal(@"C:\Games\Starfield\Data", old.StarfieldDataPath);
    }

    [Fact]
    public void CurrentSettingsKeepTheUsersChoice()
    {
        var current = JsonSerializer.Deserialize<AppSettings>(
            $"{{\"IncludeAllModVoices\":false,\"SettingsVersion\":{AppSettings.CurrentVersion}}}")!;

        Assert.False(current.Upgrade());
        Assert.False(current.IncludeAllModVoices);
    }
}
