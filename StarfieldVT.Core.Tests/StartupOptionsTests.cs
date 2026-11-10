namespace StarfieldVT.Core.Tests;

public class StartupOptionsTests
{
    [Fact]
    public void NoArgumentsMeansDefaults()
    {
        var o = StartupOptions.Parse(Array.Empty<string>());
        Assert.False(o.Verbose);
        Assert.Null(o.LogFile);
        Assert.Empty(o.Errors);
        Assert.Empty(o.Unknown);
    }

    [Theory]
    [InlineData("--verbose")]
    [InlineData("-v")]
    [InlineData("--VERBOSE")]
    public void VerboseFlag(string flag)
    {
        Assert.True(StartupOptions.Parse(new[] { flag }).Verbose);
    }

    [Fact]
    public void LogFileWithSpaceOrEquals()
    {
        var path = Path.Combine(Path.GetTempPath(), "svt.log");
        Assert.Equal(path, StartupOptions.Parse(new[] { "--log", path }).LogFile);
        Assert.Equal(path, StartupOptions.Parse(new[] { "--log=" + path }).LogFile);
    }

    [Fact]
    public void LogWithoutFileIsAnError()
    {
        var o = StartupOptions.Parse(new[] { "--log", "--verbose" });
        Assert.Null(o.LogFile);
        Assert.True(o.Verbose);
        Assert.Single(o.Errors);
    }

    [Fact]
    public void OtherArgumentsAreKeptAsUnknown()
    {
        var o = StartupOptions.Parse(new[] { "--something", "-v" });
        Assert.True(o.Verbose);
        Assert.Equal(new[] { "--something" }, o.Unknown);
    }
}
