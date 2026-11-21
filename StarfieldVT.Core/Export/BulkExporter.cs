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
    /// <param name="wholeExportPaths">
    /// When this run is the first part of a bigger export (the window's 50-file sample), the layout
    /// paths of the whole export, so a hashed name never lands on a path a later item of it needs.
    /// </param>
    public static ExportSummary Run<T>(
        IReadOnlyList<ExportItem<T>> items,
        string outFolder,
        BulkExportOptions options,
        Action<T, string> convert,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancel = default,
        IEnumerable<string>? wholeExportPaths = null)
    {
        var clock = Stopwatch.StartNew();
        var problem = FolderProblem(outFolder);
        if (problem != null) throw new IOException(problem);
        var root = Path.GetFullPath(outFolder);
        try
        {
            Directory.CreateDirectory(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(FolderMessage(root, ex.Message), ex);
        }
        Log.Information("Bulk export of {Count} files to {Folder}, layout {Layout}, list.csv {Csv}",
            items.Count, root, options.Layout, options.CsvHeader == null ? "off" : "on");

        int written = 0, kept = 0, failed = 0, done = 0, unconfirmed = 0;
        var cancelled = false;
        var failures = new List<ExportFailure>();
        var present = new List<ExportItem<T>>();

        // who the earlier list.csv says each existing file belongs to, so a file is kept only for its own item
        var plan = PathPlan.For(root, options.CsvHeader, items, wholeExportPaths);

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
                item = plan?.OwnPath(original, warn: true) ?? original;
                var path = ExportLayout.ResolveInside(root, item.RelativePath);

                if (File.Exists(path))
                {
                    kept++;
                    present.Add(item);
                    if (plan != null && !plan.Lists(item.RelativePath)) unconfirmed++;
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

        if (unconfirmed > 0)
        {
            Log.Warning("{Count} files already in {Folder} were kept although the earlier list.csv doesn't list them " +
                        "(it is missing, has other columns, or couldn't be written last time), so their items couldn't be checked. " +
                        "If this folder holds another export, export into a new folder instead", unconfirmed, root);
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

        var summary = new ExportSummary(items.Count, written, kept, failed, cancelled, clock.Elapsed.TotalSeconds, csvPath, failures)
        {
            PresentPaths = present.Select(i => i.RelativePath).ToList(),
        };
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

    /// <summary>
    /// The paths a run into this folder would use: the same as <see cref="Run{T}"/>, so an item whose
    /// path holds another item's file (by the earlier list.csv) shows its hashed name. Writes nothing;
    /// for --list. An item that would fail (no free name, or a path the folder can't hold) keeps its
    /// first path and gets the reason as its Problem.
    /// </summary>
    public static List<PlannedPath<T>> PlannedPaths<T>(IReadOnlyList<ExportItem<T>> items, string outFolder, IReadOnlyList<string>? csvHeader)
    {
        // a folder the run couldn't make: every item would fail with this message
        var problem = FolderProblem(outFolder);
        if (problem != null) return items.Select(i => new PlannedPath<T>(i, problem)).ToList();
        var root = Path.GetFullPath(outFolder);

        // with no list.csv, or no folder yet, nothing is renamed, but a path the folder can't hold still fails
        var plan = csvHeader == null || !Directory.Exists(root) ? null : PathPlan.For(root, csvHeader, items, null);
        return items.Select(i =>
        {
            try
            {
                var own = plan?.OwnPath(i, warn: false) ?? i;
                ExportLayout.ResolveInside(root, own.RelativePath); // throws for a path the run would refuse
                return new PlannedPath<T>(own, null);
            }
            catch (Exception ex)
            {
                // the run would report this item as failed with the same message
                return new PlannedPath<T>(i, ex.Message);
            }
        }).ToList();
    }

    /// <summary>
    /// Why <see cref="Run{T}"/> couldn't make or use this export folder, or null when it can (as far as
    /// can be told without writing): a name the system refuses, a folder name with a character file
    /// names can't hold (on Windows <c>&lt; &gt; : " | ? *</c>, which .NET doesn't refuse until the folder
    /// is made), or a part of the path that is a file. Writes nothing; --list and the run share it.
    /// </summary>
    public static string? FolderProblem(string outFolder)
    {
        static string Message(string folder, string reason) => FolderMessage(folder, reason);

        string root;
        try
        {
            root = Path.GetFullPath(outFolder);
        }
        catch (Exception ex)
        {
            return Message(outFolder, ex.Message);
        }

        var invalid = Path.GetInvalidFileNameChars();
        var parts = root[(Path.GetPathRoot(root) ?? "").Length..]
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.IndexOfAny(invalid) >= 0)
                return Message(root, $"the folder name \"{part}\" has a character a file or folder name can't hold");
        }

        // the nearest part of the path that exists must be a folder
        for (var dir = root; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
        {
            if (Directory.Exists(dir)) break;
            if (File.Exists(dir)) return Message(root, $"{dir} is a file");
        }

        return null;
    }

    /// <summary>
    /// The one message for an export folder that can't be made, used by <see cref="FolderProblem"/>
    /// (so <c>--list</c> shows it) and by <see cref="Run"/> when making the folder fails.
    /// </summary>
    public static string FolderMessage(string folder, string reason) =>
        $"Could not make the export folder {folder} ({reason}). Choose a folder you can write to that isn't the name of a file.";

    /// <summary>The most tries at a free hashed name before an item is reported as failed.</summary>
    private const int MaxRenames = 99;

    /// <summary>
    /// Chooses each item's path for one run: the earlier list.csv's owner of every file, every path
    /// this run's layout gives an item, and the hashed names this run has handed out already.
    /// </summary>
    private sealed class PathPlan
    {
        private readonly string _root;
        private readonly int[] _identity;
        private readonly Dictionary<string, List<string>> _owners;
        private readonly Dictionary<string, HashSet<string>> _listedFor;
        private readonly HashSet<string> _layoutPaths;
        private readonly HashSet<string> _claimed = new(StringComparer.OrdinalIgnoreCase); // every path an item of this run took, first paths too

        private PathPlan(string root, int[] identity, Dictionary<string, List<string>> owners, HashSet<string> layoutPaths)
        {
            _root = root;
            _identity = identity;
            _owners = owners;
            _layoutPaths = layoutPaths;
            _listedFor = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (path, row) in owners)
            {
                var key = Key(row);
                if (!_listedFor.TryGetValue(key, out var paths)) _listedFor[key] = paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                paths.Add(path);
            }
        }

        /// <summary>The plan for a run into <paramref name="root"/>; null when no list.csv is written (nothing is checked).</summary>
        public static PathPlan? For<T>(string root, IReadOnlyList<string>? header, IReadOnlyList<ExportItem<T>> items, IEnumerable<string>? wholeExportPaths)
        {
            if (header == null) return null;
            var layoutPaths = new HashSet<string>(items.Select(i => i.RelativePath), StringComparer.OrdinalIgnoreCase);
            if (wholeExportPaths != null) layoutPaths.UnionWith(wholeExportPaths);
            return new PathPlan(root, ExportLayout.IdentityColumns(header), EarlierOwners(Path.Combine(root, ExportCsv.FileName), header), layoutPaths);
        }

        /// <summary>The earlier list.csv names this file.</summary>
        public bool Lists(string relativePath) => _owners.ContainsKey(relativePath);

        /// <summary>
        /// The item at its own path: the first path, unless that holds a file the earlier list.csv lists
        /// under another item. Then (or when the earlier list shows this item at one of its hashed names
        /// whose file is still there) <c>&lt;name&gt;_&lt;hash&gt;.wav</c>, then <c>_2</c> ... A new hashed
        /// name skips the paths other items of this run use. An item's own earlier file is kept even
        /// when it sits on another item's layout path (that item moves instead, since the earlier list
        /// shows the file as this item's), so no item is ever exported twice, unless another item of
        /// this run already took that path (its file is then that item's). Throws when no free name
        /// is left, so another item's file is never kept.
        /// </summary>
        public ExportItem<T> OwnPath<T>(ExportItem<T> original, bool warn)
        {
            if (_owners.Count == 0) return original;
            var first = original.RelativePath;
            var hash = ExportLayout.StableHash(original.Description);
            string Candidate(int n) => n == 0 ? first : ExportLayout.WithSuffix(first, hash + (n == 1 ? "" : "_" + n));

            // 1. this item's own file from an earlier run, at its first path or a hashed name (the lowest n wins)
            if (_listedFor.TryGetValue(Key(original), out var listed))
            {
                var best = -1;
                foreach (var path in listed)
                {
                    var n = CandidateIndex(path, first, hash);
                    if (n < 0 || (best >= 0 && n >= best)) continue;
                    // a path another item of this run already took holds that item's file now, not this one's
                    if (!_claimed.Contains(path) && Exists(path)) best = n;
                }

                if (best >= 0) return Claim(original, Candidate(best), best, warn, kept: true);
            }

            // 2. the first free name: not listed under another item with its file still there
            for (var n = 0; n <= MaxRenames; n++)
            {
                var path = Candidate(n);
                if (_claimed.Contains(path) || (n > 0 && _layoutPaths.Contains(path))) continue;
                if (!_owners.TryGetValue(path, out var owner) || SameItem(owner, original) || !Exists(path))
                {
                    return Claim(original, path, n, warn, kept: false);
                }
            }

            throw new IOException(
                $"{first} and {MaxRenames} hashed names after it all hold other items' files (by list.csv); export into a new folder.");
        }

        private ExportItem<T> Claim<T>(ExportItem<T> original, string path, int n, bool warn, bool kept)
        {
            _claimed.Add(path);
            if (n == 0) return original;
            if (warn)
            {
                if (kept)
                {
                    Log.Debug("{Item} is kept at {Other}, where the earlier list.csv lists it", original.Description, path);
                }
                else
                {
                    // a hashed name is only chosen when the first path holds another item's file
                    var owner = _owners[original.RelativePath];
                    Log.Warning("{Path} is another item's file (the earlier list.csv lists {Owner} there), so {Item} goes to {Other}",
                        original.RelativePath, string.Join(", ", _identity.Where(c => c >= 0 && c < owner.Count).Select(c => owner[c])), original.Description, path);
                }
            }

            return original with { RelativePath = path };
        }

        private bool Exists(string relativePath) => File.Exists(ExportLayout.ResolveInside(_root, relativePath));

        /// <summary>
        /// Which of the item's names a path is: 0 for its first path, 1 for <c>_&lt;hash&gt;</c>, n for
        /// <c>_&lt;hash&gt;_n</c> (2 to 99), or -1 for another path. One look per listed path, so a big
        /// re-export doesn't build all 100 names for every item.
        /// </summary>
        public static int CandidateIndex(string path, string first, string hash)
        {
            if (string.Equals(path, first, StringComparison.OrdinalIgnoreCase)) return 0;
            var hashed = ExportLayout.WithSuffix(first, hash);
            if (string.Equals(path, hashed, StringComparison.OrdinalIgnoreCase)) return 1;

            var ext = Path.GetExtension(first);
            var stem = hashed[..^ext.Length] + "_";
            if (path.Length <= stem.Length + ext.Length ||
                !path.StartsWith(stem, StringComparison.OrdinalIgnoreCase) ||
                !path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return -1;
            var digits = path[stem.Length..^ext.Length];
            if (digits.Length > 2 || digits[0] == '0' || !digits.All(char.IsAsciiDigit)) return -1;
            var n = int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
            return n is >= 2 and <= MaxRenames ? n : -1;
        }

        /// <summary>The identity columns of a row (File first), joined, to find an item's earlier rows.</summary>
        private string Key(IReadOnlyList<string> row) =>
            string.Join("\u001f", _identity.Select(c => c >= 0 && c < row.Count ? row[c] : ""));

        private string Key<T>(ExportItem<T> item) => Key([item.RelativePath, .. item.CsvRow]);

        /// <summary>The earlier row and this item agree on the identity columns (ID and Source, or Plugin and Game File).</summary>
        private bool SameItem<T>(List<string> owner, ExportItem<T> item)
        {
            IReadOnlyList<string> row = [item.RelativePath, .. item.CsvRow];
            return _identity.All(c => c < 0 || (c < owner.Count && c < row.Count &&
                                                string.Equals(owner[c], row[c], StringComparison.OrdinalIgnoreCase)));
        }
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
