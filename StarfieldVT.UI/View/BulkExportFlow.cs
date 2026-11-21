using Avalonia.Controls;
using Avalonia.Platform.Storage;

using Serilog;

using StarfieldVT.Core.Export;
using StarfieldVT.UI.ViewModel;

namespace StarfieldVT.UI.View;

/// <summary>
/// A bulk export from either tab: choose a folder, lay out the files, run the Core BulkExporter on a
/// background task with progress and Cancel, and ask first when there are more than 1,000 files
/// (with a size estimate from the first 50 written).
/// </summary>
public static class BulkExportFlow
{
    /// <param name="owner">The tab, for the folder picker and the confirm dialog.</param>
    /// <param name="bar">The tab's export bar (check boxes, progress, the one-export-at-a-time gate).</param>
    /// <param name="sources">The sounds or voice lines to export.</param>
    /// <param name="what">"sounds" or "voice lines", for the texts.</param>
    /// <param name="layout">Builds the export items (ExportLayout.ForSounds or ForVoices) given Keep game folders.</param>
    /// <param name="columns">The list.csv header.</param>
    /// <param name="convert">Writes one item's WAV file (AudioConverter.ConvertWemToWav on the item's bytes).</param>
    /// <param name="setStatus">Sets the tab's status line.</param>
    public static async Task RunAsync<T>(
        Control owner,
        ExportBarViewModel bar,
        IReadOnlyList<T> sources,
        string what,
        Func<IEnumerable<T>, bool, List<ExportItem<T>>> layout,
        IReadOnlyList<string> columns,
        Action<T, string> convert,
        Action<string> setStatus)
    {
        if (ExportBarViewModel.AnyRunning)
        {
            setStatus(ExportBarViewModel.BusyText);
            return;
        }

        if (sources.Count == 0)
        {
            setStatus($"There are no {what} to export");
            return;
        }

        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"Export {sources.Count:N0} {what} to a folder",
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;
        var folder = folders[0].TryGetLocalPath()
                     ?? throw new InvalidOperationException("Please choose a folder on a local drive.");

        var keepFolders = bar.KeepFolders;
        var options = new BulkExportOptions(bar.WriteCsv ? columns : null, keepFolders ? "game folders" : "flat");
        var items = await Task.Run(() => layout(sources, keepFolders));

        if (!bar.TryBegin(items.Count))
        {
            setStatus(ExportBarViewModel.BusyText);
            return;
        }

        try
        {
            Log.Information("Window export of {Count} {What} to {Folder} ({Layout}, list.csv {Csv})",
                items.Count, what, folder, options.Layout, options.CsvHeader == null ? "off" : "on");
            setStatus(ExportPlan.ProgressText(0, items.Count));
            var progress = new Progress<ExportProgress>(p =>
            {
                bar.Done = p.Done;
                setStatus(ExportPlan.ProgressText(p.Done, items.Count));
            });
            var token = bar.Token;

            ExportSummary summary;
            if (!ExportPlan.NeedsConfirm(items.Count))
            {
                summary = await Task.Run(() => BulkExporter.Run(items, folder, options, convert, progress, token));
            }
            else
            {
                var sampleItems = items.Take(ExportPlan.SampleSize).ToList();
                // the sample's hashed names must leave the paths of the rest of the export free
                var wholePaths = items.Select(i => i.RelativePath).ToList();
                var sample = await Task.Run(() => BulkExporter.Run(sampleItems, folder, options, convert, progress, token, wholePaths));
                if (ExportPlan.SampleCancelled(sample, token))
                {
                    setStatus($"Export cancelled after {sample.Written + sample.Kept:N0} of {items.Count:N0} {what} in {folder}");
                    return;
                }

                var estimate = ExportPlan.EstimateBytes(ExportPlan.SampleSizes(folder, sample.PresentPaths), items.Count);
                var message = ExportPlan.ConfirmMessage(items.Count, what, folder, estimate, FreeSpace(folder));
                Log.Information("Asking before a big export: {Count} {What}, estimate {Estimate} bytes", items.Count, what, estimate);

                if (topLevel is not Window window || !await new ConfirmExportDialog(message).ShowDialog<bool>(window))
                {
                    Log.Information("Big export not confirmed; the first {Sample} files stay in {Folder}", sampleItems.Count, folder);
                    setStatus($"Export stopped before it started; the first {sample.Written + sample.Kept:N0} files stay in {folder}");
                    return;
                }

                var all = await Task.Run(() => BulkExporter.Run(items, folder, options, convert, progress, token));
                summary = ExportPlan.Merge(sample, all);
            }

            var status = $"{summary} to {folder}";
            if (summary.CsvPath != null) status += "; list in list.csv";
            setStatus(status);
        }
        finally
        {
            bar.End();
        }
    }

    private static long? FreeSpace(string folder)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(folder));
            return string.IsNullOrEmpty(root) ? null : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not read the free space for {Folder}", folder);
            return null;
        }
    }
}
