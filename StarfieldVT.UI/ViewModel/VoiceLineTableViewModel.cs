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

    /// <summary>Show the lines of a plugin or voice type, applying the current filter once.</summary>
    public void ShowVoiceLines(IEnumerable<VoiceLine> voiceLines, string? master, string? voiceType)
    {
        _selectionVoiceLines = voiceLines.ToList();
        SelectedMaster = master;
        SelectedVoiceType = voiceType;
        OnPropertyChanged(nameof(SelectedMaster));
        OnPropertyChanged(nameof(SelectedVoiceType));
        Log.Debug("Showing {Count} lines for master {Master}, voice type {VoiceType}",
            _selectionVoiceLines.Count, master, voiceType);
        ApplyFilter();
    }

    public void Clear() => ShowVoiceLines([], null, null);

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(VoiceLineFilterText))
        {
            // replacing the collection is far cheaper than clearing and re-adding thousands of rows
            VoiceLines = new ObservableCollection<VoiceLine>(_selectionVoiceLines);
            StatusText = $"{VoiceLines.Count:N0} lines";
            return;
        }

        try
        {
            var filteredVoiceLines = _luceneManager.FreeTextSearch(VoiceLineFilterText, SelectedMaster, SelectedVoiceType);
            Log.Debug("Filter with {FilterText} found {Count} result(s)", VoiceLineFilterText, filteredVoiceLines.Count);
            VoiceLines = new ObservableCollection<VoiceLine>(filteredVoiceLines);
            StatusText = SelectedMaster == null
                ? $"{VoiceLines.Count:N0} matches in all plugins"
                : $"{VoiceLines.Count:N0} matches";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Search for {FilterText} failed", VoiceLineFilterText);
            VoiceLines = [];
            StatusText = $"Search failed: {ex.Message}";
        }
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
