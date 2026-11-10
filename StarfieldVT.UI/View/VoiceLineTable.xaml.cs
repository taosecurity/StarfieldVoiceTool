using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Mutagen.Bethesda.Archives;

using Serilog;

using StarfieldVT.Core;
using StarfieldVT.Core.Models;

using StarfieldVT.UI.Audio;
using StarfieldVT.UI.ViewModel;

using Path = System.IO.Path;

namespace StarfieldVT.UI.View;

public partial class VoiceLineTable : UserControl
{
    /// <summary>How close to a header's edge (in pixels) a double-click counts as the column divider.</summary>
    private const double DividerHitWidth = 8;

    public VoiceLineTable()
    {
        InitializeComponent();
        // the header handles presses near its edge for resizing, so listen to handled events too
        DialogueGrid.AddHandler(DoubleTappedEvent, OnHeaderDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static DataGridColumnHeader? HeaderOf(TappedEventArgs e) =>
        (e.Source as Visual)?.FindAncestorOfType<DataGridColumnHeader>(includeSelf: true);

    /// <summary>
    /// Double-clicking the divider at a column header's edge fits that column to its contents, as in
    /// Windows Explorer and Excel. (Not the middle of the header: a click there sorts the column.)
    /// </summary>
    private void OnHeaderDoubleTapped(object? sender, TappedEventArgs e)
    {
        var header = HeaderOf(e);
        if (header == null) return;
        e.Handled = true;

        var x = e.GetPosition(header).X;
        var width = header.Bounds.Width;
        var columns = DialogueGrid.Columns.OrderBy(c => c.DisplayIndex).ToList();
        var index = columns.FindIndex(c => Equals(c.Header, header.Content));
        if (index < 0) return;

        if (x >= width - DividerHitWidth) FitColumn(columns, index);
        else if (x <= DividerHitWidth && index > 0) FitColumn(columns, index - 1);
    }

    /// <summary>Size a column to its header and the rows on screen, then keep that width.</summary>
    private void FitColumn(IReadOnlyList<DataGridColumn> columns, int index)
    {
        var column = columns[index];
        var isLast = index == columns.Count - 1;
        column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
        Dispatcher.UIThread.Post(() =>
        {
            var fitted = Math.Ceiling(column.ActualWidth);
            var others = columns.Where(c => c != column).Sum(c => c.ActualWidth);
            var room = DialogueGrid.Bounds.Width - others - 20; // leave room for the scroll bar
            // the last column fills the space left over when its text fits, as before
            column.Width = isLast && fitted <= room
                ? new DataGridLength(1, DataGridLengthUnitType.Star)
                : new DataGridLength(fitted);
            Log.Debug("Fitted column {Column} to {Width} px", column.Header, fitted);
        }, DispatcherPriority.Background);
    }

    public VoiceLineTableViewModel? ViewModel => DataContext as VoiceLineTableViewModel;

    private static IArchiveFile FindArchiveFile(VoiceLine voiceLine) =>
        VoiceArchiveManager.Shared(StarfieldManager.GetGamePath(), SettingsManager.Instance.IncludeAllModVoices)
            .FindFile(voiceLine.Filename)
        ?? throw new FileNotFoundException(
            $"Could not find {voiceLine.Filename} in the voice archives for {voiceLine.ModName}");

    private async void DialogueGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        // a double-click on the column headers is for fitting columns, not for playing a line
        if (HeaderOf(e) != null) return;

        var viewModel = ViewModel;
        var voiceLine = viewModel?.SelectedVoiceLine;
        if (viewModel == null || voiceLine == null) return;

        try
        {
            viewModel.StatusText = $"Playing {Path.GetFileName(voiceLine.Filename)}";
            // finding the file and decoding it are slow; keep the UI responsive
            await Task.Run(() => VoiceManager.Instance.PlayVoiceLine(FindArchiveFile(voiceLine)));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to play voice line {Filename} from {ModName}", voiceLine.Filename, voiceLine.ModName);
            viewModel.StatusText = $"Could not play line: {ex.Message}";
        }
    }

    private async void ExportBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel == null) return;

        var voiceLine = viewModel.SelectedVoiceLine;
        if (voiceLine == null)
        {
            viewModel.StatusText = "Select a voice line to export";
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        try
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export voice line",
                SuggestedFileName = $"{voiceLine.VoiceType}_{Path.GetFileNameWithoutExtension(voiceLine.Filename)}.wav",
                DefaultExtension = "wav",
                ShowOverwritePrompt = true,
                FileTypeChoices = [new FilePickerFileType("WAV audio") { Patterns = ["*.wav"] }]
            });
            if (file == null) return;

            var wavPath = file.TryGetLocalPath()
                          ?? throw new InvalidOperationException("Please choose a location on a local drive.");

            viewModel.StatusText = "Exporting...";
            await Task.Run(() => AudioConverter.ConvertWemToWav(FindArchiveFile(voiceLine).GetBytes(), wavPath));
            viewModel.StatusText = $"Exported to {wavPath}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export voice line {Filename}", voiceLine.Filename);
            viewModel.StatusText = $"Export failed: {ex.Message}";
        }
    }
}
