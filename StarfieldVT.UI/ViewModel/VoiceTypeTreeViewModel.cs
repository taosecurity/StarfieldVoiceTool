using System.Collections.ObjectModel;

using Serilog;

using StarfieldVT.Core;
using StarfieldVT.Core.Filesystem;
using StarfieldVT.Core.Models;


namespace StarfieldVT.UI.ViewModel;

public class VoiceTypeTreeViewModel
{
    public NotifyTaskCompletion<ObservableCollection<Master>> Masters { get; }

    /// <summary>Completes when loading finishes, fails or is cancelled. Never faults.</summary>
    public Task LoadTask { get; }

    private readonly CancellationTokenSource _cancellation = new();
    private List<Master> _searchableMasters = new();
    private string _filter = "";

    public event EventHandler<VoiceTypeTreeViewModelProgressChangedEventHandler>? ProgressChanged;

    public class VoiceTypeTreeViewModelProgressChangedEventHandler(EsmLoadingProgress progress) : EventArgs
    {
        public EsmLoadingProgress Progress
        {
            get;
            private set;
        } = progress;
    }

    public VoiceTypeTreeViewModel(string dataFolderPath, bool bustCache)
    {
        // created on the UI thread, so reports are marshalled back to it
        var progress = new Progress<EsmLoadingProgress>(value =>
            ProgressChanged?.Invoke(this, new VoiceTypeTreeViewModelProgressChangedEventHandler(value)));

        var loadTask = LoadVoiceTypeTree(dataFolderPath, bustCache, progress, _cancellation.Token);
        LoadTask = loadTask;
        Masters = new NotifyTaskCompletion<ObservableCollection<Master>>(loadTask);
    }

    public void Cancel() => _cancellation.Cancel();

    private async Task<ObservableCollection<Master>?> LoadVoiceTypeTree(string dataFolderPath, bool bustCache,
        IProgress<EsmLoadingProgress> progress, CancellationToken cancellationToken)
    {
        try
        {
            var tree = await Task.Run(() =>
            {
                var cacheManager = new VoiceLineTreeCacheManager();
                if (bustCache)
                {
                    cacheManager.BustCache();
                }

                var fingerprint = GameFingerprint.Compute(dataFolderPath);
                var treeCache = cacheManager.TryToLoadCache(fingerprint);
                if (treeCache != null)
                {
                    return treeCache;
                }

                LuceneManager.Instance().DeleteIndex();
                return new DialogueTreeBuilder().BuildTree(dataFolderPath, fingerprint, progress, cancellationToken);
            }, cancellationToken);

            _searchableMasters = tree;
            var lineCount = tree.Sum(master => master.VoiceTypes.Sum(vt => vt.VoiceLines.Count()));
            progress.Report(new EsmLoadingProgress
            {
                State = LoadState.Completed,
                Message = $"Loaded {lineCount:N0} voice lines from {tree.Count} plugins"
            });

            return new ObservableCollection<Master>(FilterMasters(tree, _filter));
        }
        catch (OperationCanceledException)
        {
            Log.Information("Loading voice types was cancelled");
            return null;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load voice types, see logs for error at {LogDir}", AppDataFolder.GetLogDir());
            progress.Report(new EsmLoadingProgress { State = LoadState.Failed, Message = e.Message });
            return null;
        }
    }

    public void FilterVoiceTypes(string searchString)
    {
        // remembered so a filter typed while loading is applied once loading finishes
        _filter = searchString;
        if (Masters.Result == null) return;

        var filteredMasters = FilterMasters(_searchableMasters, searchString);
        Log.Information("Filter with {SearchQuery} found {NumResults} results", searchString, filteredMasters.Count);

        Masters.Result.Clear();
        foreach (var master in filteredMasters)
        {
            Masters.Result.Add(master);
        }
    }

    private static List<Master> FilterMasters(List<Master> masters, string searchString)
    {
        if (string.IsNullOrEmpty(searchString)) return masters;

        return masters.Where(master =>
            master.VoiceTypes.Any(vt =>
                vt.EditorId.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
        ).Select(master =>
            new Master(master.Filename, master.VoiceTypes.Where(
                    vt => vt.EditorId.Contains(searchString, StringComparison.InvariantCultureIgnoreCase)
                ).ToList()
            )
        ).ToList();
    }
}
