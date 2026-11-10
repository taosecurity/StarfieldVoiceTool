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
            RestorePluginListWidth();
            if (SettingsManager.Instance.SoundGroupListWidth is { } soundWidth && soundWidth >= 160)
            {
                SoundEffectsView.RestoreGroupListWidth(soundWidth);
            }

            Closing += (_, _) =>
            {
                SavePluginListWidth();
                SaveSoundGroupListWidth();
            };
        }

        private void SaveSoundGroupListWidth()
        {
            var width = Math.Round(SoundEffectsView.GroupColumn.ActualWidth);
            // ActualWidth is 0 when the tab was never shown; keep the saved width then
            if (width >= 160 && width != SettingsManager.Instance.SoundGroupListWidth)
            {
                SettingsManager.Instance.SoundGroupListWidth = width;
            }
        }

        /// <summary>The sound effects are listed the first time their tab is opened, not at startup.</summary>
        private async void MainTabs_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // selection changes in the lists and tables inside the tabs bubble up to here too
            if (!ReferenceEquals(e.Source, MainTabs)) return;
            await LoadSoundsIfShown();
        }

        private async Task LoadSoundsIfShown()
        {
            if (!ReferenceEquals(MainTabs.SelectedItem, SoundEffectsTab)) return;
            if (!SettingsManager.Instance.HasValidGamePath())
            {
                SoundEffectsView.ViewModel.StatusText = GamePathHint;
                return;
            }

            try
            {
                await SoundEffectsView.ViewModel.EnsureLoadedAsync(StarfieldManager.GetGamePath());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to list sound effects");
                SoundEffectsView.ViewModel.StatusText = $"Failed to list sound effects: {ex.Message}";
            }
        }

        /// <summary>After the game path changed or the cache was deleted: list the sounds again.</summary>
        private async Task ReloadSounds()
        {
            SoundEffectsView.ViewModel.Reset();
            await LoadSoundsIfShown();
        }

        /// <summary>Put the plugin list back at the width the user dragged it to last time.</summary>
        private void RestorePluginListWidth()
        {
            if (SettingsManager.Instance.PluginListWidth is { } width && width >= 160)
            {
                MainGrid.ColumnDefinitions[0].Width = new GridLength(width);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                Log.Debug("Plugin list width restored to {Width}", width);
            }
        }

        private void SavePluginListWidth()
        {
            var width = Math.Round(MainGrid.ColumnDefinitions[0].ActualWidth);
            if (width >= 160 && width != SettingsManager.Instance.PluginListWidth)
            {
                SettingsManager.Instance.PluginListWidth = width;
                Log.Debug("Plugin list width saved: {Width}", width);
            }
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
                    selectedVoiceType.VoiceLines, selectedVoiceType.FromMaster, selectedVoiceType.EditorId,
                    _mainWindowModel.SearchBarText);
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
                // both at once: the sounds take seconds, the voice lines can take a minute
                var sounds = ReloadSounds();
                await ReloadTree(bustCache: true);
                await sounds;
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
                    master.VoiceTypes.SelectMany(vt => vt.VoiceLines), master.Filename, null,
                    _mainWindowModel.SearchBarText);
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
                    var sounds = ReloadSounds();
                    await ReloadTree(bustCache: false);
                    await sounds;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to change the game path");
                ProgressText.Text = $"Failed to change the game path: {ex.Message}";
            }
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e) => AboutWindow.OpenLogFolder(this);

        private async void ReportProblem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!await Launcher.LaunchUriAsync(new Uri(AboutInfo.IssuesUrl)))
                    ProgressText.Text = $"Couldn't open a browser. Report problems at {AboutInfo.IssuesUrl}";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Opening the issues page failed");
                ProgressText.Text = $"Couldn't open a browser. Report problems at {AboutInfo.IssuesUrl}";
            }
        }

        private async void About_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await new AboutWindow().ShowDialog(this);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to show the About window");
            }
        }
    }
}
