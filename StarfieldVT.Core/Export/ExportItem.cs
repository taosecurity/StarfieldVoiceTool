namespace StarfieldVT.Core.Export;

/// <summary>
/// One file in a bulk export: where it goes (relative to the export folder), its row in list.csv,
/// and the source the caller converts (a SoundEffect or a VoiceLine).
/// </summary>
/// <param name="RelativePath">The output path below the export folder, e.g. "WPN\Hand\Microgun\WPN_Fire_12.wav".</param>
/// <param name="CsvRow">The list.csv cells after the File column, in the layout's column order.</param>
/// <param name="Source">The sound or voice line to convert.</param>
/// <param name="Description">How the log names the item, e.g. "sound 12 from Starfield - WwiseSounds01.ba2".</param>
public sealed record ExportItem<T>(string RelativePath, IReadOnlyList<string> CsvRow, T Source, string Description);

/// <summary>The path a run would give an item (for --list); Problem says why the run would fail it, or is null.</summary>
public sealed record PlannedPath<T>(ExportItem<T> Item, string? Problem);

/// <summary>The lines --list prints after its first rows about the items that would fail.</summary>
public static class PlannedPathReport
{
    /// <summary>
    /// Failing items below the first <paramref name="shown"/> rows (at most <paramref name="shown"/> of
    /// them, each with its WILL FAIL reason), then one closing line; nothing when no item would fail.
    /// </summary>
    public static List<string> FailureLines<T>(IReadOnlyList<PlannedPath<T>> planned, int shown)
    {
        var lines = new List<string>();
        var failing = planned.Count(p => p.Problem != null);
        if (failing == 0) return lines;

        var below = planned.Skip(shown).Where(p => p.Problem != null).ToList();
        if (below.Count > 0)
        {
            lines.Add($"Items below the first {shown} rows that would fail:");
            lines.AddRange(below.Take(shown).Select(p => $"  {p.Item.RelativePath}  ({p.Item.Description}) WILL FAIL: {p.Problem}"));
            if (below.Count > shown) lines.Add($"  ... and {below.Count - shown:N0} more that would fail");
        }

        lines.Add(below.Count > shown
            ? $"{failing:N0} of them would fail; the reasons of the ones shown follow WILL FAIL above. Narrow the options (--group, --plugin or --search) to see the rest."
            : $"{failing:N0} of them would fail; each one's reason follows WILL FAIL above.");
        return lines;
    }
}

/// <summary>Progress after each file: how many are done out of the total, and the last file's path.</summary>
public sealed record ExportProgress(int Done, int Total, string RelativePath);

/// <summary>One file that failed, with the reason.</summary>
public sealed record ExportFailure(string RelativePath, string Description, string Message);

/// <summary>What a bulk export did.</summary>
public sealed record ExportSummary(
    int Total,
    int Written,
    int Kept,
    int Failed,
    bool Cancelled,
    double Seconds,
    string? CsvPath,
    IReadOnlyList<ExportFailure> Failures)
{
    /// <summary>
    /// The paths (below the export folder) of the files that exist at the end, written or kept, in
    /// item order. A path may be a hashed name rather than the item's layout path.
    /// </summary>
    public IReadOnlyList<string> PresentPaths { get; init; } = [];

    /// <summary>Files neither written, kept nor failed because the export was cancelled first.</summary>
    public int NotReached => Total - Written - Kept - Failed;

    /// <summary>One line for the status bar or the console.</summary>
    public override string ToString()
    {
        var text = $"Exported {Written:N0} of {Total:N0} files";
        if (Kept > 0) text += $"; {Kept:N0} already there (kept)";
        if (Failed > 0) text += $"; {Failed:N0} failed (see the log)";
        if (Cancelled) text += $"; cancelled with {NotReached:N0} not done";
        return text + $" in {Seconds:N1} s";
    }
}
