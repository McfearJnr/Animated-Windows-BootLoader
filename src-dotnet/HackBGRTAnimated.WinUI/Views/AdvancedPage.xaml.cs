using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using HackBGRTAnimated.Core.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackBGRTAnimated.WinUI.Views;

public sealed partial class AdvancedPage : Page
{
    private readonly App _app;
    private bool _legacyUnlocked;

    public AdvancedPage()
    {
        InitializeComponent();
        _app = (App)Application.Current;
    }

    private async void RevealLegacy_OnClick(object sender, RoutedEventArgs e)
    {
        if (_legacyUnlocked)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Show Legacy Actions",
            Content = "Legacy actions can alter firmware boot entries. Continue?",
            PrimaryButtonText = "Show",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _legacyUnlocked = true;
        DangerGateCard.Visibility = Visibility.Collapsed;
        LegacyActionsCard.Visibility = Visibility.Visible;
    }

    private async void LaunchLegacySetup_OnClick(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_app.Services.Paths.SetupExePath))
        {
            await ShowErrorAsync("setup.exe not found in repository root.");
            return;
        }

        Process.Start(new ProcessStartInfo(_app.Services.Paths.SetupExePath)
        {
            UseShellExecute = true,
            WorkingDirectory = _app.Services.Paths.RepoRoot
        });
    }

    private void OpenRepo_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_app.Services.Paths.RepoRoot}\"")
        {
            UseShellExecute = true
        });
    }

    private void ShowRawBcdEdit_OnClick(object sender, RoutedEventArgs e)
    {
        var result = ProcessRunner.Run("bcdedit", "/enum firmware /v", _app.Services.Paths.RepoRoot);
        DiagnosticsBox.Text = $"Command: bcdedit /enum firmware /v{Environment.NewLine}" +
                              $"Exit code: {result.ExitCode}{Environment.NewLine}{Environment.NewLine}" +
                              result.CombinedOutput;
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Error",
            Content = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}
