using System.ComponentModel;
using System.Runtime.CompilerServices;

using Mutagen.Bethesda.Archives;

using Serilog;

using StarfieldVT.Core.Sounds;

namespace StarfieldVT.UI.ViewModel;

/// <summary>
/// The Sound Effects tab: every Wwise sound the game can load, filtered by the group on the left and
/// the search box. The index is built the first time the tab is opened (a few seconds) and kept
/// for the session; changing the game path or deleting the cache builds it again.
/// </summary>
public sealed class SoundEffectsViewModel : INotifyPropertyChanged
{
    private SoundIndexResult? _index;
    private string? _indexedPath;
    private Task? _loading;

    private List<SoundGroup> _groups = [];
    public List<SoundGroup> Groups
    {
        get => _groups;
        private set { _groups = value; OnPropertyChanged(); }
    }

    private SoundGroup? _selectedGroup;
    public SoundGroup? SelectedGroup
    {
        get => _selectedGroup;
        set { _selectedGroup = value; OnPropertyChanged(); ApplyFilter(); }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    private List<SoundEffect> _visibleSounds = [];
    public List<SoundEffect> VisibleSounds
    {
        get => _visibleSounds;
        private set { _visibleSounds = value; OnPropertyChanged(); }
    }

    private string _statusText = "Open this tab to list the game's sound effects.";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool IsLoaded => _index != null;

    /// <summary>Build the index for this Data folder unless it is already built (or being built).</summary>
    public Task EnsureLoadedAsync(string dataPath)
    {
        if (_loading != null && string.Equals(_indexedPath, dataPath, StringComparison.OrdinalIgnoreCase))
            return _loading;

        _indexedPath = dataPath;
        return _loading = LoadAsync(dataPath);
    }

    /// <summary>Forget the index, e.g. after the game path changed; it is built again when next needed.</summary>
    public void Reset()
    {
        _loading = null;
        _indexedPath = null;
        _index = null;
        Groups = [];
        VisibleSounds = [];
        StatusText = "Open this tab to list the game's sound effects.";
    }

    private async Task LoadAsync(string dataPath)
    {
        StatusText = "Reading sound archives...";
        try
        {
            var index = await Task.Run(() => SoundIndex.Build(dataPath));
            if (!string.Equals(_indexedPath, dataPath, StringComparison.OrdinalIgnoreCase)) return; // reset meanwhile

            _index = index;
            Groups = SoundCatalog.Groups(index.Sounds);
            _selectedGroup = Groups[0];
            OnPropertyChanged(nameof(SelectedGroup));
            ApplyFilter();
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to list sound effects in {DataPath}", dataPath);
            StatusText = $"Failed to list sound effects: {e.Message}";
            _loading = null; // let the next visit try again
        }
    }

    private void ApplyFilter()
    {
        var index = _index;
        if (index == null) return;

        var group = SelectedGroup;
        var terms = SoundCatalog.Terms(SearchText);
        VisibleSounds = index.Sounds
            .Where(s => (group == null || group.Contains(s)) && SoundCatalog.Matches(s, terms))
            .ToList();

        var mods = index.Sounds.Count(s => s.FromMod);
        StatusText = VisibleSounds.Count == index.Sounds.Count
            ? $"{index.Sounds.Count:N0} sounds ({mods:N0} from mods). Double-click to play."
            : $"Showing {VisibleSounds.Count:N0} of {index.Sounds.Count:N0} sounds. Double-click to play.";
    }

    public IArchiveFile FindFile(SoundEffect sound) =>
        _index != null && _index.Files.TryGetValue(sound.Id, out var file)
            ? file
            : throw new FileNotFoundException($"Sound {sound.Id} is no longer in the index; reopen the tab.");

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
