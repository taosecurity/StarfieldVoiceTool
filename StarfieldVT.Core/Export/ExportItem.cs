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
