using System.Diagnostics;

using Serilog;

namespace StarfieldVT.Core.Export;

/// <summary>Settings for one bulk export.</summary>
/// <param name="CsvHeader">The list.csv header (File first); null writes no list.csv.</param>
/// <param name="Layout">How the log describes the layout, e.g. "game folders" or "flat".</param>
public sealed record BulkExportOptions(IReadOnlyList<string>? CsvHeader, string Layout = "game folders");

/// <summary>
/// Exports many files one at a time: creates the folders, keeps files already there, converts the
/// rest through a delegate (so BnkExtractor stays out of Core), carries on after a failure, and
/// writes list.csv of the files that exist at the end, also after a cancel.
/// </summary>
public static class BulkExporter
{
    /// <summary>The ending of a file being written; it is renamed when the conversion finishes.</summary>
    public const string PartialSuffix = ".part";

    /// <param name="items">The files to export, from <see cref="ExportLayout"/>.</param>
    /// <param name="outFolder">The export folder (created if missing).</param>
    /// <param name="options">The CSV header and the layout name for the log.</param>
    /// <param name="convert">Writes one item's WAV file to the given full path.</param>
    /// <param name="progress">Told after each file; may be null.</param>
    /// <param name="cancel">Checked before each file; a file in progress finishes first.</param>
    public static ExportSummary Run<T>(
        IReadOnlyList<ExportItem<T>> items,
        string outFolder,
        BulkExportOptions options,
        Action<T, string> convert,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancel = default)
    {
        var clock = Stopwatch.StartNew();
        var root = Path.GetFullPath(outFolder);
        try
        {
            Directory.CreateDirectory(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"Could not make the export folder {root} ({ex.Message}). Choose a folder you can write to that isn't the name of a file.", ex);
        }
        Log.Information("Bulk export of {Count} files to {Folder}, layout {Layout}, list.csv {Csv}",
            items.Count, root, options.Layout, options.CsvHeader == null ? "off" : "on");

        int written = 0, kept = 0, failed = 0, done = 0;
        var cancelled = false;
        var failures = new List<ExportFailure>();
        var present = new List<ExportItem<T>>();

        // who the earlier list.csv says each existing file belongs to, so a file is kept only for its own item
        var owners = options.CsvHeader == null ? null : EarlierOwners(Path.Combine(root, ExportCsv.FileName), options.CsvHeader);
        var identity = options.CsvHeader == null ? [] : ExportLayout.IdentityColumns(options.CsvHeader);

        foreach (var original in items)
        {
            if (cancel.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            var item = original;
            string? partial = null;
            try
            {
                var path = ExportLayout.ResolveInside(root, item.RelativePath);
                for (var n = 1;
                     n < 100 && owners != null && File.Exists(path) && owners.TryGetValue(item.RelativePath, out var owner) &&
                     !SameItem(owner, item, identity);
                     n++)
                {
                    var other = ExportLayout.WithSuffix(original.RelativePath,
                        ExportLayout.StableHash(original.Description) + (n == 1 ? "" : "_" + n));
                    Log.Warning("{Path} is another item's file (the earlier list.csv lists {Owner} there), so {Item} goes to {Other}",
                        item.RelativePath, string.Join(", ", identity.Where(c => c < owner.Count).Select(c => owner[c])), item.Description, other);
                    item = item with { RelativePath = other };
                    path = ExportLayout.ResolveInside(root, other);
                }

                if (File.Exists(path))
                {
                    kept++;
                    present.Add(item);
                    Log.Debug("Kept {Path} (already there)", item.RelativePath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    partial = path + PartialSuffix;
                    convert(item.Source, partial);
                    File.Move(partial, path);
                    partial = null;
                    written++;
                    present.Add(item);
                    Log.Debug("Wrote {Path} ({Item})", item.RelativePath, item.Description);
                }
            }
            catch (Exception ex)
            {
                failed++;
                failures.Add(new ExportFailure(item.RelativePath, item.Description, ex.Message));
                Log.Error(ex, "Failed to export {Item} to {Path}", item.Description, item.RelativePath);
                if (partial != null) TryDelete(partial);
            }

            done++;
            progress?.Report(new ExportProgress(done, items.Count, item.RelativePath));
        }

        string? csvPath = null;
        if (options.CsvHeader != null && done == 0)
        {
            // nothing was reached (cancelled at once, or no items): leave an earlier list as it is
            var existing = Path.Combine(root, ExportCsv.FileName);
            csvPath = File.Exists(existing) ? existing : null;
            Log.Information("No file was reached, so list.csv was left as it was ({CsvPath})", csvPath ?? "none");
        }
        else if (options.CsvHeader != null)
        {
            csvPath = Path.Combine(root, ExportCsv.FileName);
            try
            {
                var rows = present.Select(i => (IReadOnlyList<string>)[i.RelativePath, .. i.CsvRow]).ToList();
                ExportCsv.Write(csvPath, options.CsvHeader, MergeWithEarlierList(csvPath, root, options.CsvHeader, rows));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not write {CsvPath}; check that it isn't open in Excel and that the drive has space", csvPath);
                failures.Add(new ExportFailure(ExportCsv.FileName, "list.csv", ex.Message));
                failed++;
                csvPath = null;
            }
        }

        var summary = new ExportSummary(items.Count, written, kept, failed, cancelled, clock.Elapsed.TotalSeconds, csvPath, failures);
        Log.Information("Bulk export finished: {Written} written, {Kept} kept, {Failed} failed, cancelled {Cancelled}, {Seconds:F1} s, list {CsvPath}",
            summary.Written, summary.Kept, summary.Failed, summary.Cancelled, summary.Seconds, csvPath ?? "none");
        return summary;
    }

    /// <summary>
    /// The rows of an earlier list.csv in this folder whose files still exist, with this run's rows
    /// put in place of the same files and the new ones added at the end. So a second export into the
    /// same folder (another search, or a rerun after a cancel) keeps the earlier files listed.
    /// </summary>
    public static List<IReadOnlyList<string>> MergeWithEarlierList(
        string csvPath, string root, IReadOnlyList<string> header, List<IReadOnlyList<string>> rows)
    {
        var earlier = ReadEarlierList(csvPath, header, warn: true);
        if (earlier == null) return rows;

        var fresh = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows) fresh[row[0]] = row;

        var merged = new List<IReadOnlyList<string>>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int carried = 0, gone = 0, badRows = 0;
        foreach (var old in earlier.Skip(1))
        {
            if (old.Count == 0 || old[0].Length == 0 || !used.Add(old[0])) continue;
            if (fresh.TryGetValue(old[0], out var replacement))
            {
                merged.Add(replacement);
                continue;
            }

            if (old.Count != header.Count)
            {
                badRows++;
                continue;
            }

            bool exists;
            try
            {
                exists = File.Exists(ExportLayout.ResolveInside(root, old[0]));
            }
            catch (Exception)
            {
                // a path outside the folder, or one Windows can't read (a hand-edited list)
                badRows++;
                continue;
            }

            if (exists)
            {
                merged.Add(old);
                carried++;
            }
            else
            {
                gone++;
            }
        }

        merged.AddRange(rows.Where(r => used.Add(r[0])));
        Log.Information("list.csv keeps {Carried} rows from the earlier list (dropped {Gone} whose files are gone and {Bad} that couldn't be read)",
            carried, gone, badRows);
        return merged;
    }

    /// <summary>The earlier list.csv's rows (header first) when it has these columns; otherwise null.</summary>
    private static List<List<string>>? ReadEarlierList(string csvPath, IReadOnlyList<string> header, bool warn)
    {
        if (!File.Exists(csvPath)) return null;

        List<List<string>> earlier;
        try
        {
            earlier = ExportCsv.Parse(File.ReadAllText(csvPath));
        }
        catch (Exception ex)
        {
            if (warn) Log.Warning(ex, "Could not read the earlier {CsvPath}; it is replaced by this run's list", csvPath);
            return null;
        }

        if (earlier.Count == 0 || !earlier[0].SequenceEqual(header))
        {
            if (warn)
            {
                Log.Warning("The earlier {CsvPath} has other columns ({Columns}), so it is replaced by this run's list. " +
                            "Export sounds and voice lines to different folders to keep both lists",
                    csvPath, earlier.Count == 0 ? "none" : string.Join(", ", earlier[0]));
            }

            return null;
        }

        return earlier;
    }

    /// <summary>Each file the earlier list.csv names, with its row (the first row wins); empty when there is no usable list.</summary>
    private static Dictionary<string, List<string>> EarlierOwners(string csvPath, IReadOnlyList<string> header)
    {
        var owners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ReadEarlierList(csvPath, header, warn: false)?.Skip(1) ?? [])
        {
            if (row.Count == header.Count && row[0].Length > 0) owners.TryAdd(row[0], row);
        }

        return owners;
    }

    /// <summary>The earlier row and this item agree on the identity columns (ID and Source, or Plugin and Game File).</summary>
    private static bool SameItem<T>(List<string> owner, ExportItem<T> item, int[] identity)
    {
        IReadOnlyList<string> row = [item.RelativePath, .. item.CsvRow];
        return identity.All(c => c < 0 || (c < owner.Count && c < row.Count &&
                                           string.Equals(owner[c], row[c], StringComparison.OrdinalIgnoreCase)));
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not remove the unfinished file {Path}", path);
        }
    }
}
