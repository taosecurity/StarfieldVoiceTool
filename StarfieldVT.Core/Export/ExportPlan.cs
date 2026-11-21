using System.Globalization;

namespace StarfieldVT.Core.Export;

/// <summary>
/// The window's export rules, kept here so they can be tested: the button text, when to ask before
/// a big export, the size estimate from a sample of written files, and the status line texts.
/// </summary>
public static class ExportPlan
{
    /// <summary>Exports of more files than this ask for confirmation first.</summary>
    public const int ConfirmAbove = 1000;

    /// <summary>How many files a big export writes before it asks, to estimate the total size.</summary>
    public const int SampleSize = 50;

    /// <summary>The "Export All Shown (N)" button text.</summary>
    public static string AllShownLabel(int count) =>
        string.Create(CultureInfo.CurrentCulture, $"Export All Shown ({count:N0})");

    /// <summary>True when an export of this many files should ask first.</summary>
    public static bool NeedsConfirm(int count) => count > ConfirmAbove;

    /// <summary>The status line during an export, e.g. "Exporting 1,204 of 35,491...".</summary>
    public static string ProgressText(int done, int total) =>
        string.Create(CultureInfo.CurrentCulture, $"Exporting {done:N0} of {total:N0}...");

    /// <summary>
    /// The sizes in bytes of the sample's files that exist (written now or already there), in order;
    /// files that failed or weren't reached are left out.
    /// </summary>
    public static List<long> SampleSizes<T>(string outFolder, IEnumerable<ExportItem<T>> sample)
    {
        var root = Path.GetFullPath(outFolder);
        var sizes = new List<long>();
        foreach (var item in sample)
        {
            try
            {
                var file = new FileInfo(ExportLayout.ResolveInside(root, item.RelativePath));
                if (file.Exists) sizes.Add(file.Length);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                // a name that can't be resolved failed in the export too; it isn't part of the estimate
            }
        }

        return sizes;
    }

    /// <summary>The estimated total size of <paramref name="total"/> files from the average of the sample, or null with no sample.</summary>
    public static long? EstimateBytes(IReadOnlyCollection<long> sampleSizes, int total)
    {
        if (sampleSizes.Count == 0 || total <= 0) return null;
        var average = sampleSizes.Sum(s => (double)s) / sampleSizes.Count;
        return (long)Math.Round(average * total);
    }

    /// <summary>A size for people: "512 bytes", "3.4 MB", "41.9 GB" (1 KB = 1,024 bytes).</summary>
    public static string FormatBytes(long bytes)
    {
        string[] units = ["KB", "MB", "GB", "TB"];
        if (bytes < 1024) return string.Create(CultureInfo.CurrentCulture, $"{bytes:N0} bytes");
        double value = bytes;
        var unit = -1;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.CurrentCulture, $"{value:N1} {units[unit]}");
    }

    /// <summary>
    /// The question before a big export. <paramref name="estimate"/> is null when no sample file was
    /// written; <paramref name="freeBytes"/> is null when the drive's free space is unknown.
    /// </summary>
    public static string ConfirmMessage(int total, string what, string folder, long? estimate, long? freeBytes)
    {
        var text = string.Create(CultureInfo.CurrentCulture, $"Export {total:N0} {what} to {folder}?");
        text += estimate == null
            ? $"{Environment.NewLine}{Environment.NewLine}None of the first {SampleSize} files could be written, so there is no size estimate; see the log."
            : string.Create(CultureInfo.CurrentCulture,
                $"{Environment.NewLine}{Environment.NewLine}From the first {SampleSize} files, the export will take about {FormatBytes(estimate.Value)}.");
        if (freeBytes != null)
        {
            text += $" The drive has {FormatBytes(freeBytes.Value)} free.";
            if (estimate != null && estimate.Value > freeBytes.Value)
                text += " That may not be enough: free some space or choose another drive.";
        }

        return text + $"{Environment.NewLine}{Environment.NewLine}Large exports can take several minutes; Cancel stops after the current file.";
    }

    /// <summary>
    /// One summary for a big export done in two runs: the sample, then the whole list (which keeps
    /// the sample's files, so they count as written, not kept).
    /// </summary>
    public static ExportSummary Merge(ExportSummary sample, ExportSummary all)
    {
        var moved = Math.Min(sample.Written, all.Kept);
        return all with
        {
            Written = all.Written + moved,
            Kept = all.Kept - moved,
            Seconds = sample.Seconds + all.Seconds,
        };
    }
}
