using StarfieldVT.Core.Filesystem;

namespace StarfieldVT.Core.Tests;

public class TempDirTests
{
    [Fact]
    public void Windows_UsesPerUserTempFolder()
    {
        var dir = AppDataFolder.GetTempDir(true, "ignored");
        Assert.Equal(Path.Combine(Path.GetTempPath(), "StarfieldVT"), dir);
    }

    [UnixFact]
    public void Unix_UsesPrivateFolderUnderAppData_NotSharedTmp()
    {
        var root = Path.Combine(Path.GetTempPath(), "svt-tempdir-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dir = AppDataFolder.GetTempDir(false, root);
            Assert.Equal(Path.Combine(root, "temp"), dir);
            Assert.NotEqual(Path.Combine(Path.GetTempPath(), "StarfieldVT"), dir);
            Assert.True(Directory.Exists(dir));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(dir));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void NonWindows_NeverReturnsSharedTmpName()
    {
        var root = Path.Combine(Path.GetTempPath(), "svt-tempdir-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (OperatingSystem.IsWindows()) return; // the folder creation uses Unix modes
            Assert.DoesNotContain(Path.Combine(Path.GetTempPath(), "StarfieldVT"), AppDataFolder.GetTempDir(false, root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
