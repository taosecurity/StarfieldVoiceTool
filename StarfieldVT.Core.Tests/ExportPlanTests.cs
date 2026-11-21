using System.Globalization;

using StarfieldVT.Core.Export;

namespace StarfieldVT.Core.Tests;

/// <summary>The window's export rules: button text, the over-1,000 rule, the size estimate and the merged summary.</summary>
public class ExportPlanTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "svt-plan-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public ExportPlanTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static ExportItem<int> Item(string path) => new(path, [], 0, path);

    [Fact]
    public void ButtonTextShowsTheCountWithSeparators()
    {
        Assert.Equal("Export All Shown (0)", ExportPlan.AllShownLabel(0));
        Assert.Equal("Export All Shown (35,491)", ExportPlan.AllShownLabel(35491));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1000, false)]
    [InlineData(1001, true)]
    [InlineData(35491, true)]
    public void OnlyExportsOverAThousandAsk(int count, bool asks)
    {
        Assert.Equal(asks, ExportPlan.NeedsConfirm(count));
    }

    [Fact]
    public void ProgressTextMatchesThePlan()
    {
        Assert.Equal("Exporting 1,204 of 35,491...", ExportPlan.ProgressText(1204, 35491));
    }

    [Fact]
    public void EstimateIsTheSampleAverageTimesTheCount()
    {
        Assert.Equal(3000L, ExportPlan.EstimateBytes([100, 200], 20));
        Assert.Null(ExportPlan.EstimateBytes([], 20));
        Assert.Null(ExportPlan.EstimateBytes([100], 0));
    }

    [Fact]
    public void SampleSizesCountOnlyFilesThatExist()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "WPN"));
        File.WriteAllBytes(Path.Combine(_dir, "WPN", "a.wav"), new byte[10]);
        File.WriteAllBytes(Path.Combine(_dir, "b.wav"), new byte[30]);

        var sizes = ExportPlan.SampleSizes(_dir,
            [Path.Combine("WPN", "a.wav"), "missing.wav", "b.wav", Path.Combine("..", "outside.wav")]);

        Assert.Equal([10L, 30L], sizes);
    }

    [Theory]
    [InlineData(0L, "0 bytes")]
    [InlineData(1023L, "1,023 bytes")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(3565158L, "3.4 MB")]
    [InlineData(44989935411L, "41.9 GB")]
    public void SizesReadLikeExplorer(long bytes, string text)
    {
        Assert.Equal(text, ExportPlan.FormatBytes(bytes));
    }

    [Fact]
    public void ConfirmMessageGivesCountEstimateAndFreeSpace()
    {
        var text = ExportPlan.ConfirmMessage(35491, "sounds", @"D:\Out", 44989935411L, 2L * 1024 * 1024 * 1024 * 1024);
        Assert.Contains(@"Export 35,491 sounds to D:\Out?", text);
        Assert.Contains("about 41.9 GB", text);
        Assert.Contains("The drive has 2.0 TB free.", text);
        Assert.DoesNotContain("not be enough", text);
    }

    [Fact]
    public void ConfirmMessageWarnsWhenTheDriveIsTooSmall()
    {
        var text = ExportPlan.ConfirmMessage(35491, "sounds", @"D:\Out", 44989935411L, 1024L * 1024 * 1024);
        Assert.Contains("may not be enough", text);
    }

    [Fact]
    public void ConfirmMessageWithoutASampleSaysSo()
    {
        var text = ExportPlan.ConfirmMessage(2000, "voice lines", @"D:\Out", null, null);
        Assert.Contains("no size estimate", text);
        Assert.DoesNotContain("free", text);
    }

    [Fact]
    public void MergedSummaryCountsTheSampleAsWritten()
    {
        var sample = new ExportSummary(50, 48, 1, 1, false, 2.0, null, []);
        // the full run keeps the sample's 49 files and retries the one that failed
        var all = new ExportSummary(2000, 1950, 49, 1, false, 60.0, @"D:\Out\list.csv", []);

        var merged = ExportPlan.Merge(sample, all);

        Assert.Equal(2000, merged.Total);
        Assert.Equal(1998, merged.Written);
        Assert.Equal(1, merged.Kept);
        Assert.Equal(1, merged.Failed);
        Assert.Equal(62.0, merged.Seconds);
        Assert.Equal(0, merged.NotReached);
        Assert.Equal(@"D:\Out\list.csv", merged.CsvPath);
    }

    [Fact]
    public void BigExportInTwoRunsMatchesOneRun()
    {
        var items = Enumerable.Range(0, 120).Select(i => new ExportItem<int>($"f{i}.wav", [i.ToString(CultureInfo.InvariantCulture)], i, $"item {i}")).ToList();
        var options = new BulkExportOptions(["File", "N"]);
        void Convert(int n, string path) => File.WriteAllBytes(path, new byte[n + 1]);

        var sample = BulkExporter.Run(items.Take(ExportPlan.SampleSize).ToList(), _dir, options, (Action<int, string>)Convert);
        var sizes = ExportPlan.SampleSizes(_dir, sample.PresentPaths);
        var all = BulkExporter.Run(items, _dir, options, (Action<int, string>)Convert);
        var merged = ExportPlan.Merge(sample, all);

        Assert.Equal(ExportPlan.SampleSize, sizes.Count);
        Assert.Equal(120, merged.Written);
        Assert.Equal(0, merged.Kept);
        Assert.Equal(121, File.ReadAllLines(Path.Combine(_dir, ExportCsv.FileName)).Length);
    }
}
