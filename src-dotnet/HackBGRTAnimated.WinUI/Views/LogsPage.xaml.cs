using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace HackBGRTAnimated.WinUI.Views;

public sealed partial class LogsPage : Page
{
    private readonly App _app;

    public LogsPage()
    {
        InitializeComponent();
        _app = (App)Application.Current;
        LogTypeCombo.SelectedIndex = 0;
        RefreshLogs();
    }

    private void RefreshLogs()
    {
        LogsPathText.Text = $"AppData logs folder: {_app.Services.Paths.LogsDir}";
        UpdateLogContent();
        LogStatusText.Text = $"Updated {DateTime.Now:T}";
    }

    private void UpdateLogContent()
    {
        var selected = (LogTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Boot Log";
        LogContentBox.Text = selected switch
        {
            "Setup Log" => _app.Services.Logs.ReadSetupLog(),
            "Action Log" => _app.Services.Logs.ReadLastActionLog(),
            "GUI Log" => _app.Services.Logs.ReadGuiLog(),
            _ => _app.Services.Logs.ReadLastBootLog(),
        };
    }

    private void LogTypeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateLogContent();

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshLogs();

    private void OpenLogsFolder_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_app.Services.Paths.LogsDir}\"")
        {
            UseShellExecute = true
        });
    }

    private async void CopyLog_OnClick(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(LogContentBox.Text ?? string.Empty);
        Clipboard.SetContent(package);
        LogStatusText.Text = "Log copied to clipboard.";

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "HackBGRT-Animated",
            Content = "Current log copied to clipboard.",
            CloseButtonText = "OK",
        };

        await dialog.ShowAsync();
    }
}
