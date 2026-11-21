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

    [Fact]
    public void SettingsFromBefore0100TurnBothExportBoxesOn()
    {
        // what 0.9.1 wrote: version 2, no export settings; no version bump is needed for them
        var old = JsonSerializer.Deserialize<AppSettings>(
            "{\"IncludeAllModVoices\":true,\"SoundGroupListWidth\":240,\"SettingsVersion\":2}")!;

        Assert.False(old.Upgrade());
        Assert.True(old.ExportKeepFolders);
        Assert.True(old.ExportWriteCsv);
    }

    [Fact]
    public void ExportBoxesAreRemembered()
    {
        var json = JsonSerializer.Serialize(new AppSettings { ExportKeepFolders = false, ExportWriteCsv = false });
        var back = JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.False(back.ExportKeepFolders);
        Assert.False(back.ExportWriteCsv);
    }
}
