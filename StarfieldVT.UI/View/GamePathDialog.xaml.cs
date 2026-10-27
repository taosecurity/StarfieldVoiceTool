using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

using Serilog;

using StarfieldVT.Core;

namespace StarfieldVT.UI.View;

/// <summary>
/// Asks for the Starfield Data folder. The path can always be typed or pasted, because the system
/// folder picker isn't available everywhere (e.g. Linux without xdg-desktop-portal or GTK, or WSL).
/// Closes with the validated Data folder path, or null when cancelled.
/// </summary>
public partial class GamePathDialog : Window
{
    public GamePathDialog() : this(null)
    {
    }

    public GamePathDialog(string? currentPath)
    {
        InitializeComponent();
        PathBox.Text = currentPath ?? string.Empty;
        if (!OperatingSystem.IsWindows())
        {
            PathBox.Watermark = "e.g. ~/.steam/steam/steamapps/common/Starfield/Data";
        }

        BrowseButton.IsEnabled = StorageProvider.CanPickFolder;
        Opened += (_, _) => PathBox.Focus();
    }

    private void ShowMessage(string message)
    {
        MessageText.Text = message;
        MessageText.IsVisible = true;
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Starfield Data Folder",
                AllowMultiple = false
            });

            var picked = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (picked != null)
            {
                PathBox.Text = picked;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "The system folder picker failed");
            ShowMessage("The folder picker isn't available here; type or paste the path instead.");
        }
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        var typed = PathBox.Text ?? string.Empty;
        string? dataPath;
        try
        {
            dataPath = SettingsManager.NormalizeDataFolder(typed);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            Log.Warning(ex, "Could not check {Path}", typed);
            dataPath = null;
        }

        if (dataPath == null)
        {
            ShowMessage($"No Starfield.esm found in \"{SettingsManager.CleanUpTypedPath(typed)}\" or its Data folder. " +
                        "Check the path, and that this account can read it.");
            return;
        }

        Close(dataPath);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
