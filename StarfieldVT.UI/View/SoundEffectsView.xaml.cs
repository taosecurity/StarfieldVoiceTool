using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

using Serilog;

using StarfieldVT.Core.Export;
using StarfieldVT.Core.Sounds;
using StarfieldVT.UI.Audio;
using StarfieldVT.UI.ViewModel;

using Path = System.IO.Path;

namespace StarfieldVT.UI.View;

public partial class SoundEffectsView : UserControl
{
    public SoundEffectsView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public SoundEffectsViewModel ViewModel { get; } = new();

    /// <summary>The group list's column, so the window can remember its width.</summary>
    public ColumnDefinition GroupColumn => SoundGrid.ColumnDefinitions[0];

    public void RestoreGroupListWidth(double width)
    {
        SoundGrid.ColumnDefinitions[0].Width = new GridLength(width);
        SoundGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
    }

    private List<SoundEffect> SelectedSounds() => SoundTable.SelectedItems.OfType<SoundEffect>().ToList();

    private async void SoundTable_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        // a double-click on a column header resizes or sorts; it isn't a request to play
        if ((e.Source as Visual)?.FindAncestorOfType<DataGridColumnHeader>(includeSelf: true) != null) return;
        await PlaySelected();
    }

    private async void PlayBtn_OnClick(object? sender, RoutedEventArgs e) => await PlaySelected();

    private void StopBtn_OnClick(object? sender, RoutedEventArgs e) => AudioOutputManager.Instance.StopSound();

    private async Task PlaySelected()
    {
        var sound = SoundTable.SelectedItem as SoundEffect;
        if (sound == null)
        {
            ViewModel.StatusText = "Select a sound to play";
            return;
        }

        try
        {
            ViewModel.StatusText = $"Playing {sound.Name} ({sound.Id}, from {sound.Source})";
            Log.Information("Playing sound {Id} {Name} from {Source}", sound.Id, sound.Name, sound.Source);
            // reading and converting are slow for long sounds; keep the UI responsive
            await Task.Run(() => VoiceManager.Instance.PlayVoiceLine(ViewModel.FindFile(sound)));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to play sound {Id} from {Source}", sound.Id, sound.Source);
            ViewModel.StatusText = $"Could not play sound {sound.Id}: {ex.Message}";
        }
    }

    private async void CopyIdBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        var sounds = SelectedSounds();
        if (sounds.Count == 0)
        {
            ViewModel.StatusText = "Select one or more sounds to copy their IDs";
            return;
        }

        await CopyText(string.Join(Environment.NewLine, sounds.Select(s => s.Id)),
            sounds.Count == 1 ? $"Copied ID {sounds[0].Id}" : $"Copied {sounds.Count} IDs");
    }

    private async void CopyEventsBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        var events = SelectedSounds().SelectMany(s => s.Events).Distinct(StringComparer.Ordinal).ToList();
        if (events.Count == 0)
        {
            ViewModel.StatusText = "The selected sounds have no events listed";
            return;
        }

        await CopyText(string.Join(Environment.NewLine, events), $"Copied {events.Count} event name(s)");
    }

    private async Task CopyText(string text, string done)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard
                            ?? throw new InvalidOperationException("No clipboard is available.");
            await clipboard.SetTextAsync(text);
            ViewModel.StatusText = done;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Copy to clipboard failed");
            ViewModel.StatusText = $"Copy failed: {ex.Message}";
        }
    }

    private async void ExportBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        var sounds = SelectedSounds();
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        if (sounds.Count == 0)
        {
            ViewModel.StatusText = "Select one or more sounds to export";
            return;
        }

        if (ExportBarViewModel.AnyRunning)
        {
            ViewModel.StatusText = ExportBarViewModel.BusyText;
            return;
        }

        try
        {
            if (sounds.Count == 1)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export sound",
                    SuggestedFileName = SoundCatalog.ExportFileName(sounds[0]),
                    DefaultExtension = "wav",
                    ShowOverwritePrompt = true,
                    FileTypeChoices = [new FilePickerFileType("WAV audio") { Patterns = ["*.wav"] }]
                });
                if (file == null) return;

                var wavPath = file.TryGetLocalPath()
                              ?? throw new InvalidOperationException("Please choose a location on a local drive.");
                ViewModel.StatusText = "Exporting...";
                await Task.Run(() => Export(sounds[0], wavPath));
                ViewModel.StatusText = $"Exported to {wavPath}";
                return;
            }

            await BulkExport(sounds);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Export failed");
            ViewModel.StatusText = $"Export failed: {ex.Message}";
        }
    }

    private async void ExportAllBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await BulkExport(ViewModel.VisibleSounds);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Export failed");
            ViewModel.StatusText = $"Export failed: {ex.Message}";
        }
    }

    private void CancelExportBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        Log.Information("Export cancel requested");
        ViewModel.StatusText = "Cancelling after the current file...";
        ViewModel.ExportBar.Cancel();
    }

    /// <summary>Several sounds to a folder, through the shared Core exporter (game folders or flat, list.csv).</summary>
    private Task BulkExport(IReadOnlyList<SoundEffect> sounds) =>
        BulkExportFlow.RunAsync(this, ViewModel.ExportBar, sounds, "sounds",
            ExportLayout.ForSounds, ExportLayout.SoundColumns,
            (sound, path) => AudioConverter.ConvertWemToWav(ViewModel.FindFile(sound).GetBytes(), path),
            text => ViewModel.StatusText = text);

    private void Export(SoundEffect sound, string wavPath)
    {
        Log.Information("Exporting sound {Id} {Name} from {Source} to {WavPath}", sound.Id, sound.Name, sound.Source, wavPath);
        AudioConverter.ConvertWemToWav(ViewModel.FindFile(sound).GetBytes(), wavPath);
    }
}
