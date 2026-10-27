using Avalonia;
using Avalonia.Controls;

using Serilog;

using StarfieldVT.Core;
using StarfieldVT.Core.Models;

using StarfieldVT.UI.Events;
using StarfieldVT.UI.ViewModel;

namespace StarfieldVT.UI.View;

public partial class VoiceTypeTree : UserControl
{
    public class VoiceTypeTreeProgressChangedEventHandler : EventArgs
    {
        public EsmLoadingProgress Progress { get; private set; }

        public VoiceTypeTreeProgressChangedEventHandler(EsmLoadingProgress progress)
        {
            Progress = progress;
        }
    }

    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    public VoiceTypeTreeViewModel? VoiceTypeTreeViewModel { get; private set; }

    public VoiceTypeTree()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Load the voice type tree for a data folder. Any load already in progress is cancelled and
    /// awaited first, so two builds never write to the cache and search index at once.
    /// </summary>
    public async Task ReloadAsync(string dataFolderPath, bool bustCache = false)
    {
        await _reloadLock.WaitAsync();
        try
        {
            var previous = VoiceTypeTreeViewModel;
            if (previous != null)
            {
                previous.ProgressChanged -= VoiceTypeTreeViewModelOnProgressChanged;
                previous.Cancel();
                await previous.LoadTask;
            }

            VoiceArchiveManager.ResetShared();

            var viewModel = new VoiceTypeTreeViewModel(dataFolderPath, bustCache);
            viewModel.ProgressChanged += VoiceTypeTreeViewModelOnProgressChanged;
            viewModel.FilterVoiceTypes(SearchText);
            VoiceTypeTreeViewModel = viewModel;
            EsmTreeView.DataContext = viewModel;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private void VoiceTypeTreeViewModelOnProgressChanged(object? sender,
        VoiceTypeTreeViewModel.VoiceTypeTreeViewModelProgressChangedEventHandler e)
    {
        ProgressChanged?.Invoke(this, new VoiceTypeTreeProgressChangedEventHandler(e.Progress));
    }

    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<VoiceTypeTree, string>(nameof(SearchText), defaultValue: "");

    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    static VoiceTypeTree()
    {
        SearchTextProperty.Changed.AddClassHandler<VoiceTypeTree>(OnSearchTextChanged);
    }

    public event VoiceTypeSelectedHandler? VoiceTypeSelected;
    public event MasterSelectedHandler? MasterSelected;
    public event EventHandler<VoiceTypeTreeProgressChangedEventHandler>? ProgressChanged;

    public delegate void VoiceTypeSelectedHandler(object sender, VoiceTypeSelectedArgs<IVoiceTypeTreeItem> e);
    public delegate void MasterSelectedHandler(object sender, MasterSelectedArgs<IVoiceTypeTreeItem> e);

    private void EsmTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;

        var selectedItem = e.AddedItems[0];
        var oldItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;

        switch (selectedItem)
        {
            case VoiceType selectedVoiceType:
                VoiceTypeSelected?.Invoke(this,
                    new VoiceTypeSelectedArgs<IVoiceTypeTreeItem>(
                        oldItem as IVoiceTypeTreeItem ?? selectedVoiceType,
                        selectedVoiceType));
                break;
            case Master master:
                MasterSelected?.Invoke(this,
                    new MasterSelectedArgs<IVoiceTypeTreeItem>(
                        oldItem as IVoiceTypeTreeItem ?? master,
                        master));
                break;
        }
    }

    private static void OnSearchTextChanged(VoiceTypeTree voiceTypeTree, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not string newSearchQuery)
        {
            Log.Error("The search query for the Voice Type Tree View was null");
            return;
        }

        voiceTypeTree.VoiceTypeTreeViewModel?.FilterVoiceTypes(newSearchQuery);
    }
}
