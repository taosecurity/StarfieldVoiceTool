using Avalonia.Controls;
using Avalonia.Interactivity;

namespace StarfieldVT.UI.View;

/// <summary>Asks before an export of more than 1,000 files. Closes with true to go on, false to stop.</summary>
public partial class ConfirmExportDialog : Window
{
    public ConfirmExportDialog() : this("")
    {
    }

    public ConfirmExportDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void Yes_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void No_Click(object? sender, RoutedEventArgs e) => Close(false);
}
