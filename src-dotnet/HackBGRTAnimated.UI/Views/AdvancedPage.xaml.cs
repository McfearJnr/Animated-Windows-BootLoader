using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HackBGRTAnimated.UI.Services;

namespace HackBGRTAnimated.UI.Views;

public partial class AdvancedPage : UserControl
{
    private readonly AppServices _services;

    public AdvancedPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
    }

    private void LaunchLegacy_OnClick(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_services.Paths.SetupExePath))
        {
            MessageBox.Show("setup.exe not found.", "Advanced", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo(_services.Paths.SetupExePath) { UseShellExecute = true, WorkingDirectory = _services.Paths.RepoRoot });
    }

    private void OpenRepo_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_services.Paths.RepoRoot}\"") { UseShellExecute = true });
    }
}
