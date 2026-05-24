using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using HackBGRTAnimated.UI.Services;

namespace HackBGRTAnimated.UI.Views;

public partial class DashboardPage : UserControl
{
    private readonly AppServices _services;

    public DashboardPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var s = _services.Status.GetStatus();
        InstalledText.Text = s.Installed ? "Yes" : "No";
        EfiFolderText.Text = s.EfiFolderFound ? "Found" : "Missing";
        BootEntryText.Text = s.BootEntryFound ? "Found" : "Missing";
        BootOrderText.Text = s.InBootOrder ? "Yes" : "No";
        DefaultText.Text = s.IsDefault ? "Yes" : "No";
        ActiveThemeText.Text = s.ActiveTheme;
        EfiPathText.Text = s.EfiFolderPath;
        AppDataText.Text = s.AppDataRoot;
        ThemesPathText.Text = s.AppDataThemesPath;
        AdminText.Text = s.IsAdmin ? "Running as administrator" : "Not elevated";
    }

    private void RunAction(string name, Action action, bool confirm = false)
    {
        if (confirm && MessageBox.Show($"Are you sure you want to {name}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            action();
            RefreshStatus();
            MessageBox.Show($"{name} completed.", "HackBGRT-Animated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void InstallUpdate_OnClick(object sender, RoutedEventArgs e) => RunAction("Install / Update", _services.Operations.InstallOrUpdate, true);
    private void RepairBootEntry_OnClick(object sender, RoutedEventArgs e) => RunAction("Repair Boot Entry", _services.Operations.RepairBootEntry, true);
    private void MakeDefault_OnClick(object sender, RoutedEventArgs e) => RunAction("Make HackBGRT-Animated Default", _services.Operations.MakeAnimatedDefault, true);
    private void RestoreWindowsFirst_OnClick(object sender, RoutedEventArgs e) => RunAction("Restore Windows Boot Manager First", _services.Operations.RestoreWindowsFirst, true);
    private void Disable_OnClick(object sender, RoutedEventArgs e) => RunAction("Disable", _services.Operations.DisableAnimated, true);

    private void Uninstall_OnClick(object sender, RoutedEventArgs e)
    {
        RunAction("Uninstall", _services.Operations.UninstallAnimated, true);
    }

    private void RestartAdmin_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _services.Operations.RestartAsAdministrator();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestartPc_OnClick(object sender, RoutedEventArgs e) => RunAction("Restart PC", _services.Operations.RestartPc, true);

    private void OpenAppData_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_services.Paths.AppDataRoot}\"") { UseShellExecute = true });
    }

    private void OpenThemes_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_services.Paths.ThemesDir}\"") { UseShellExecute = true });
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshStatus();
}
