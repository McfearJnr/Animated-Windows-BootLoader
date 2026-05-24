using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using HackBGRTAnimated.UI.Services;

namespace HackBGRTAnimated.UI.Views;

public partial class LogsPage : UserControl
{
    private readonly AppServices _services;

    public LogsPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            LogsPathText.Text = $"Logs folder: {_services.Paths.LogsDir}";
            BootLogBox.Text = _services.Logs.ReadLastBootLog();
            SetupLogBox.Text = _services.Logs.ReadSetupLog();
            ActionLogBox.Text = _services.Logs.ReadLastActionLog();
            GuiLogBox.Text = _services.Logs.ReadGuiLog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Log error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => Refresh();

    private void OpenLogs_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_services.Paths.LogsDir}\"") { UseShellExecute = true });
    }
}
