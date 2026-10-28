using System.ComponentModel;
using System.Runtime.CompilerServices;

using Avalonia.Controls;
using Avalonia.Interactivity;

using Serilog;

using StarfieldVT.Core;
using StarfieldVT.Core.Filesystem;
using StarfieldVT.Core.Models;
using StarfieldVT.UI.Audio;
using StarfieldVT.UI.Events;
using StarfieldVT.UI.View;
using StarfieldVT.UI.ViewModel;

using VoiceType = StarfieldVT.Core.Models.VoiceType;

namespace StarfieldVT
{
    public sealed class MainWindowModel : INotifyPropertyChanged
    {
        public VoiceLineTableViewModel? VoiceLineTableViewModel { get; private init; }

        private string _searchBarText = "";
        public string SearchBarText
        {
            get => _searchBarText;
            set
            {
                _searchBarText = value;
                OnPropertyChanged();
            }
        }

        public static MainWindowModel GetMainWindowModel()
        {
            var mainWindowModel = new MainWindowModel
            {
                VoiceLineTableViewModel = new VoiceLineTableViewModel(),
                SearchBarText = "",
            };

            return mainWindowModel;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow : Window
    {
        private const string GamePathHint = "Starfield Data folder not set. Use File > Set Game Path.";

        private readonly MainWindowModel _mainWindowModel = MainWindowModel.GetMainWindowModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _mainWindowModel;
            IncludeAllModVoicesMenuItem.IsChecked = SettingsManager.Instance.IncludeAllModVoices;
            _mainWindowModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainWindowModel.SearchBarText)) ShowVoiceTypeFilterInTable();
            };
        }

        /// <summary>
        /// The voice type filter box drives the table as well as the tree: typing shows every line from
        /// the matching voice types, and clearing it empties the table until something is selected.
        /// </summary>
        private void ShowVoiceTypeFilterInTable()
        {
            var table = _mainWindowModel.VoiceLineTableViewModel;
            var tree = VoiceTypeTree.VoiceTypeTreeViewModel;
            if (table == null || tree?.Masters.Result == null) return;

            var voiceTypeFilter = _mainWindowModel.SearchBarText;
            if (string.IsNullOrWhiteSpace(voiceTypeFilter))
            {
                table.Clear();
                return;
            }

            table.ShowVoiceTypeFilter(tree.GetVoiceLinesMatching(voiceTypeFilter), voiceTypeFilter);
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            try
            {
                // the tree is only loaded once we know where the game is
                if (!await EnsureGamePathConfigured())
                {
                    ProgressText.Text = GamePathHint;
                    return;
                }

                await ReloadTree(bustCache: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Startup failed");
                ProgressText.Text = $"Startup failed: {ex.Message}";
            }
        }

        private void SetLoadingState()
        {
            TreeBuilderProgressBar.IsEnabled = true;
            TreeBuilderProgressBar.IsVisible = true;
            TreeBuilderProgressBar.IsIndeterminate = true;
            ProgressText.Text = "Loading voice lines...";
        }

        private async Task ReloadTree(bool bustCache)
        {
            SetLoadingState();
            _mainWindowModel.VoiceLineTableViewModel?.Clear();
            await VoiceTypeTree.ReloadAsync(StarfieldManager.GetGamePath(), bustCache);
        }

        private async Task<bool> EnsureGamePathConfigured()
        {
            var settings = SettingsManager.Instance;

            if (settings.HasValidGamePath())
                return true;

            var autoPath = settings.TryAutoDetectGamePath();
            if (autoPath != null && SettingsManager.IsDataFolder(autoPath))
            {
                settings.StarfieldDataPath = autoPath;
                return true;
            }

            return await PromptForGamePath();
        }

        private async Task<bool> PromptForGamePath()
        {
            // the dialog validates the folder, so a returned path always contains Starfield.esm
            var dataPath = await new GamePathDialog(SettingsManager.Instance.StarfieldDataPath)
                .ShowDialog<string?>(this);
            if (dataPath == null)
            {
                return false;
            }

            SettingsManager.Instance.StarfieldDataPath = dataPath;
            return true;
        }

        private void VoiceTypeTree_OnVoiceTypeSelected(object sender, VoiceTypeSelectedArgs<IVoiceTypeTreeItem> e)
        {
            if (e.NewValue is VoiceType selectedVoiceType)
            {
                _mainWindowModel.VoiceLineTableViewModel?.ShowVoiceLines(
                    selectedVoiceType.VoiceLines, selectedVoiceType.FromMaster, selectedVoiceType.EditorId);
            }
        }

        private void VoiceTypeTree_OnProgressChanged(object? sender, VoiceTypeTree.VoiceTypeTreeProgressChangedEventHandler e)
        {
            var progress = e.Progress;
            switch (progress.State)
            {
                case LoadState.Completed:
                    TreeBuilderProgressBar.IsVisible = false;
                    ProgressText.Text = progress.Message ?? "Loaded";
                    // apply a voice type filter typed while loading
                    if (!string.IsNullOrWhiteSpace(_mainWindowModel.SearchBarText))
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(ShowVoiceTypeFilterInTable);
                    }
                    break;
                case LoadState.Failed:
                    TreeBuilderProgressBar.IsVisible = false;
                    ProgressText.Text =
                        $"Failed to load voice lines: {progress.Message} (logs: {AppDataFolder.GetLogDir()})";
                    break;
                default:
                    TreeBuilderProgressBar.IsVisible = true;
                    TreeBuilderProgressBar.IsIndeterminate = progress.Total <= 0;
                    TreeBuilderProgressBar.Maximum = Math.Max(1, progress.Total);
                    TreeBuilderProgressBar.Value = progress.Num;
                    ProgressText.Text = progress.Message ??
                                        $"Loading {progress.EsmName} (plugin {progress.PluginNumber} of {progress.PluginCount}): " +
                                        $"quest {progress.Num:N0} of {progress.Total:N0}";
                    break;
            }
        }

        private async void DeleteCache_Click(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.Instance.HasValidGamePath())
            {
                ProgressText.Text = GamePathHint;
                return;
            }

            try
            {
                await ReloadTree(bustCache: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to rebuild the cache");
                ProgressText.Text = $"Failed to rebuild the cache: {ex.Message}";
            }
        }

        private void StopButton_OnClick(object sender, RoutedEventArgs e)
        {
            AudioOutputManager.Instance.StopSound();
        }

        private void VoiceTypeTree_OnMasterSelected(object sender, MasterSelectedArgs<IVoiceTypeTreeItem> masterSelectedArgs)
        {
            if (masterSelectedArgs.NewValue is Master master)
            {
                _mainWindowModel.VoiceLineTableViewModel?.ShowVoiceLines(
                    master.VoiceTypes.SelectMany(vt => vt.VoiceLines), master.Filename, null);
            }
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        private async void IncludeAllModVoices_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Instance;
            settings.IncludeAllModVoices = !settings.IncludeAllModVoices;
            IncludeAllModVoicesMenuItem.IsChecked = settings.IncludeAllModVoices;
            if (!settings.HasValidGamePath()) return;

            try
            {
                // the option is part of the cache fingerprint, so this rebuilds the tree
                await ReloadTree(bustCache: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reload after changing the mod voices option");
                ProgressText.Text = $"Failed to reload: {ex.Message}";
            }
        }

        private async void SetGamePath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // the cache fingerprint includes the data folder, so a new path rebuilds automatically
                if (await PromptForGamePath())
                {
                    await ReloadTree(bustCache: false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to change the game path");
                ProgressText.Text = $"Failed to change the game path: {ex.Message}";
            }
        }
    }
}
