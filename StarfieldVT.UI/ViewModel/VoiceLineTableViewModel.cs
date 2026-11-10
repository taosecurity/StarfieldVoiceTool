using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Serilog;
using StarfieldVT.Core;
using StarfieldVT.Core.Models;

namespace StarfieldVT.UI.ViewModel;

public sealed class VoiceLineTableViewModel : INotifyPropertyChanged
{
    private readonly LuceneManager _luceneManager = LuceneManager.Instance();

    // every line in the current selection, shown when there's no filter text
    private List<VoiceLine> _selectionVoiceLines = [];

    // the dialogue search only finds lines among those shown (plugin AND voice type filter AND text)
    private SearchScope _scope = SearchScope.Everything;

    private VoiceLine? _selectedVoiceLine;
    public VoiceLine? SelectedVoiceLine
    {
        get => _selectedVoiceLine;
        set
        {
            if (SetField(ref _selectedVoiceLine, value) && value != null)
            {
                Log.Information("Chose voice line {Filename}", value.Filename);
            }
        }
    }

    /// <summary>The plugin of the current selection, or null to search every plugin.</summary>
    public string? SelectedMaster { get; private set; }

    /// <summary>The voice type of the current selection, or null for all voice types in the plugin.</summary>
    public string? SelectedVoiceType { get; private set; }

    /// <summary>
    /// When set, the table shows voice types whose name contains this text (from the voice type
    /// filter box) instead of a single tree selection.
    /// </summary>
    public string? VoiceTypeFilter { get; private set; }

    private ObservableCollection<VoiceLine> _voiceLines = [];
    public ObservableCollection<VoiceLine> VoiceLines
    {
        get => _voiceLines;
        private set => SetField(ref _voiceLines, value);
    }

    private string _voiceLineFilterText = string.Empty;
    public string VoiceLineFilterText
    {
        get => _voiceLineFilterText;
        set
        {
            if (SetField(ref _voiceLineFilterText, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    /// <summary>
    /// Show the lines of a plugin or voice type, applying the current filter once. Pass the voice type
    /// filter box's text when the tree is filtered: the lines are then already narrowed to it, and the
    /// status line says so.
    /// </summary>
    public void ShowVoiceLines(IEnumerable<VoiceLine> voiceLines, string? master, string? voiceType,
        string? voiceTypeFilter = null)
    {
        SetSelection(voiceLines, master, voiceType,
            string.IsNullOrWhiteSpace(voiceTypeFilter) ? null : voiceTypeFilter);
    }

    /// <summary>Show the lines of every voice type whose name contains <paramref name="voiceTypeFilter"/>.</summary>
    public void ShowVoiceTypeFilter(IEnumerable<VoiceLine> voiceLines, string voiceTypeFilter)
    {
        SetSelection(voiceLines, master: null, voiceType: null, voiceTypeFilter);
    }

    public void Clear() => ShowVoiceLines([], null, null);

    private void SetSelection(IEnumerable<VoiceLine> voiceLines, string? master, string? voiceType, string? voiceTypeFilter)
    {
        _selectionVoiceLines = voiceLines.ToList();
        // an empty table with nothing chosen searches every plugin; anything chosen limits the search to it
        _scope = master == null && voiceType == null && voiceTypeFilter == null
            ? SearchScope.Everything
            : SearchScope.Of(_selectionVoiceLines);
        SelectedMaster = master;
        SelectedVoiceType = voiceType;
        VoiceTypeFilter = voiceTypeFilter;
        OnPropertyChanged(nameof(SelectedMaster));
        OnPropertyChanged(nameof(SelectedVoiceType));
        OnPropertyChanged(nameof(VoiceTypeFilter));
        Log.Debug("Showing {Count} lines for master {Master}, voice type {VoiceType}, voice type filter {VoiceTypeFilter}",
            _selectionVoiceLines.Count, master, voiceType, voiceTypeFilter);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(VoiceLineFilterText))
        {
            // replacing the collection is far cheaper than clearing and re-adding thousands of rows
            VoiceLines = new ObservableCollection<VoiceLine>(_selectionVoiceLines);
            StatusText = $"{VoiceLines.Count:N0} lines{Where()}";
            return;
        }

        try
        {
            var filteredVoiceLines = _scope.Restrict(
                _luceneManager.FreeTextSearch(VoiceLineFilterText, SelectedMaster, SelectedVoiceType));

            VoiceLines = new ObservableCollection<VoiceLine>(filteredVoiceLines);
            Log.Debug("Filter with {FilterText} found {Count} result(s) in master {Master}, voice type {VoiceType}, voice type filter {VoiceTypeFilter}",
                VoiceLineFilterText, VoiceLines.Count, SelectedMaster, SelectedVoiceType, VoiceTypeFilter);
            StatusText = _scope.IsEverything
                ? $"{VoiceLines.Count:N0} matches in all plugins"
                : $"{VoiceLines.Count:N0} matches{Where()}";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Search for {FilterText} failed", VoiceLineFilterText);
            VoiceLines = [];
            StatusText = $"Search failed: {ex.Message}";
        }
    }

    /// <summary>What the table is limited to, for the status line, e.g. " in X.esm, voice types matching "sarah"".</summary>
    private string Where()
    {
        var parts = new List<string>();
        if (SelectedVoiceType != null) parts.Add($"voice type {SelectedVoiceType}");
        if (SelectedMaster != null) parts.Add(SelectedMaster);
        if (VoiceTypeFilter != null && SelectedVoiceType == null) parts.Add($"voice types matching \"{VoiceTypeFilter}\"");
        return parts.Count == 0 ? "" : " in " + string.Join(", ", parts);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
