using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

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
    public VoiceLineTable()
    {
        InitializeComponent();
    }

    public VoiceLineTableViewModel? ViewModel => DataContext as VoiceLineTableViewModel;

    private static IArchiveFile FindArchiveFile(VoiceLine voiceLine) =>
        VoiceArchiveManager.Shared(StarfieldManager.GetGamePath()).FindFile(voiceLine.Filename)
        ?? throw new FileNotFoundException(
            $"Could not find {voiceLine.Filename} in the voice archives for {voiceLine.ModName}");

    private async void DialogueGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
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
