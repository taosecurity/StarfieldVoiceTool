using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

using Serilog;

using StarfieldVT.Core;
using StarfieldVT.Core.Filesystem;

namespace StarfieldVT;

/// <summary>Help → About: version, credits, license, links and where the settings and logs live.</summary>
public partial class AboutWindow : Window
{
    private readonly string _version = AboutInfo.Version(typeof(AboutWindow).Assembly);

    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {_version}";
        DescriptionText.Text = AboutInfo.Description;
        MaintainerText.Text = AboutInfo.Maintainer;
        CreditsText.Text = AboutInfo.Credits;
        LicenseText.Text = AboutInfo.License;
        DataPathText.Text = SettingsManager.Instance.StarfieldDataPath ?? "(not set: use File > Set Game Path)";
        AppDataText.Text = AppDataFolder.GetAppDataFolder();
    }

    private void ShowMessage(string text)
    {
        MessageText.Text = text;
        MessageText.IsVisible = true;
    }

    private async void Open(string what, Func<Task<bool>> launch)
    {
        try
        {
            if (!await launch())
            {
                Log.Warning("The system could not open {What}", what);
                ShowMessage($"Couldn't open {what} on this system.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Opening {What} failed", what);
            ShowMessage($"Couldn't open {what}: {ex.Message}");
        }
    }

    private void OpenUrl(string url) => Open(url, () => Launcher.LaunchUriAsync(new Uri(url)));

    private void Project_Click(object? sender, RoutedEventArgs e) => OpenUrl(AboutInfo.ProjectUrl);
    private void Release_Click(object? sender, RoutedEventArgs e) => OpenUrl(AboutInfo.LatestReleaseUrl);
    private void Original_Click(object? sender, RoutedEventArgs e) => OpenUrl(AboutInfo.OriginalUrl);

    private void Notices_Click(object? sender, RoutedEventArgs e)
    {
        var path = AboutInfo.NoticesPath;
        if (!File.Exists(path))
        {
            // e.g. a development build; the release zips always include it
            OpenUrl(AboutInfo.ProjectUrl + "/blob/main/THIRD-PARTY-NOTICES.md");
            return;
        }
        Open(path, () => Launcher.LaunchFileInfoAsync(new FileInfo(path)));
    }

    private void OpenLogs_Click(object? sender, RoutedEventArgs e) => OpenLogFolder(this);

    /// <summary>Open the log folder in the file manager (also used by Help → Open Log Folder).</summary>
    public static async void OpenLogFolder(TopLevel topLevel)
    {
        var dir = AppDataFolder.GetLogDir();
        try
        {
            Directory.CreateDirectory(dir);
            if (!await topLevel.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(dir)))
                Log.Warning("The system could not open the log folder {Dir}", dir);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Opening the log folder {Dir} failed", dir);
        }
    }

    private async void CopyDetails_Click(object? sender, RoutedEventArgs e)
    {
        var details = AboutInfo.Details(_version, SettingsManager.Instance.StarfieldDataPath,
            SettingsManager.Instance.IncludeAllModVoices);
        try
        {
            if (Clipboard == null) throw new InvalidOperationException("no clipboard available");
            await Clipboard.SetTextAsync(details);
            ShowMessage("Copied. Paste it into your bug report, along with the newest log file.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Copying the details to the clipboard failed");
            ShowMessage("Couldn't copy to the clipboard. The details are:" + Environment.NewLine + details);
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
