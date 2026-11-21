using System.ComponentModel;
using System.Runtime.CompilerServices;

using StarfieldVT.Core;
using StarfieldVT.Core.Export;

namespace StarfieldVT.UI.ViewModel;

/// <summary>
/// The export bar of one tab: the "Export All Shown (N)" text, the two check boxes (saved in the
/// settings and shared by both tabs), and the progress and Cancel button while an export runs.
/// Only one export runs at a time across both tabs.
/// </summary>
public sealed class ExportBarViewModel : INotifyPropertyChanged
{
    private static readonly object GateLock = new();
    private static ExportBarViewModel? _running;

    /// <summary>Raised when either tab changes a check box, so the other tab shows it too.</summary>
    private static event Action? SettingsChanged;

    public ExportBarViewModel()
    {
        // the view models live as long as the window, so the handler is never removed
        SettingsChanged += () =>
        {
            OnPropertyChanged(nameof(KeepFolders));
            OnPropertyChanged(nameof(WriteCsv));
        };
    }

    public bool KeepFolders
    {
        get => SettingsManager.Instance.ExportKeepFolders;
        set
        {
            if (SettingsManager.Instance.ExportKeepFolders == value) return;
            SettingsManager.Instance.ExportKeepFolders = value;
            SettingsChanged?.Invoke();
        }
    }

    public bool WriteCsv
    {
        get => SettingsManager.Instance.ExportWriteCsv;
        set
        {
            if (SettingsManager.Instance.ExportWriteCsv == value) return;
            SettingsManager.Instance.ExportWriteCsv = value;
            SettingsChanged?.Invoke();
        }
    }

    private int _shownCount;
    /// <summary>How many rows the tab shows; sets the button text.</summary>
    public int ShownCount
    {
        get => _shownCount;
        set
        {
            if (_shownCount == value) return;
            _shownCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AllShownText));
        }
    }

    public string AllShownText => ExportPlan.AllShownLabel(ShownCount);

    private bool _isExporting;
    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (_isExporting == value) return;
            _isExporting = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsExporting;

    private int _done;
    public int Done
    {
        get => _done;
        set { if (_done != value) { _done = value; OnPropertyChanged(); } }
    }

    private int _total = 1;
    public int Total
    {
        get => _total;
        set { if (_total != value) { _total = value; OnPropertyChanged(); } }
    }

    private CancellationTokenSource? _cancel;

    /// <summary>The token of the running export.</summary>
    public CancellationToken Token => _cancel?.Token ?? CancellationToken.None;

    /// <summary>
    /// Start an export on this tab. Returns false (and starts nothing) while another export runs on
    /// either tab. Call <see cref="End"/> when it finishes.
    /// </summary>
    public bool TryBegin(int total)
    {
        lock (GateLock)
        {
            if (_running != null) return false;
            _running = this;
        }

        _cancel = new CancellationTokenSource();
        Done = 0;
        Total = Math.Max(1, total);
        IsExporting = true;
        return true;
    }

    /// <summary>The running export stops after the current file.</summary>
    public void Cancel() => _cancel?.Cancel();

    public void End()
    {
        IsExporting = false;
        _cancel?.Dispose();
        _cancel = null;
        lock (GateLock)
        {
            if (_running == this) _running = null;
        }
    }

    /// <summary>True while an export runs on either tab.</summary>
    public static bool AnyRunning
    {
        get
        {
            lock (GateLock) return _running != null;
        }
    }

    /// <summary>What to say when the other tab's export is still running.</summary>
    public const string BusyText = "Another export is running; wait for it to finish or cancel it first.";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
