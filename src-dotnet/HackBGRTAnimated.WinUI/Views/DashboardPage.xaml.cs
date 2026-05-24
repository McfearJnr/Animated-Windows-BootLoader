using HackBGRTAnimated.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackBGRTAnimated.WinUI.Views;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage()
    {
        InitializeComponent();
        var services = ((App)Application.Current).Services;
        _viewModel = new DashboardViewModel(services);
        DataContext = _viewModel;
        RefreshView();
    }

    private void RefreshView()
    {
        _viewModel.Refresh();
        AdminInfoBar.IsOpen = !_viewModel.Status.IsAdmin;
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshView();

    private async void InstallOrUpdate_OnClick(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Install / Update", "Install or update HackBGRT-Animated EFI files and repair boot entry?", () => ((App)Application.Current).Services.Operations.InstallOrUpdate());
    }

    private async void RepairBootEntry_OnClick(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Repair Boot Entry", "Repair the HackBGRT-Animated firmware entry?", () => ((App)Application.Current).Services.Operations.RepairBootEntry());
    }

    private async void MakeDefault_OnClick(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Make Default", "Make HackBGRT-Animated first in boot order?", () => ((App)Application.Current).Services.Operations.MakeAnimatedDefault());
    }

    private async void RestoreWindowsFirst_OnClick(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Restore Windows First", "Move Windows Boot Manager to first position?", () => ((App)Application.Current).Services.Operations.RestoreWindowsFirst());
    }

    private async void Disable_OnClick(object sender, RoutedEventArgs e)
    {
        await RunActionAsync("Disable", "Disable HackBGRT-Animated in active boot order without deleting files?", () => ((App)Application.Current).Services.Operations.DisableAnimated());
    }

    private async void Uninstall_OnClick(object sender, RoutedEventArgs e)
    {
        var confirmTyped = await PromptForTextAsync("Uninstall", "Type REMOVE to uninstall HackBGRT-Animated.");
        if (!string.Equals(confirmTyped, "REMOVE", StringComparison.Ordinal))
        {
            await ShowErrorAsync("Uninstall cancelled: confirmation text did not match.");
            return;
        }

        try
        {
            ((App)Application.Current).Services.Operations.UninstallAnimated();
            RefreshView();
            await ShowMessageAsync("Uninstall completed.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private void RestartAdminButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ((App)Application.Current).Services.Operations.RestartAsAdministrator();
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            _ = ShowErrorAsync(ex.Message);
        }
    }

    private async Task RunActionAsync(string title, string confirmation, Action action)
    {
        if (!await ConfirmAsync(title, confirmation))
        {
            return;
        }

        try
        {
            action();
            RefreshView();
            await ShowMessageAsync($"{title} completed.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = "Confirm",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "HackBGRT-Animated",
            Content = message,
            CloseButtonText = "OK",
        };
        await dialog.ShowAsync();
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
        };
        await dialog.ShowAsync();
    }

    private async Task<string?> PromptForTextAsync(string title, string instruction)
    {
        var textBox = new TextBox
        {
            PlaceholderText = "Type here",
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = instruction, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(textBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = panel,
            PrimaryButtonText = "Confirm",
            CloseButtonText = "Cancel",
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? textBox.Text : null;
    }
}
