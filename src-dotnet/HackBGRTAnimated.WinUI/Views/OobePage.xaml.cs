using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HackBGRTAnimated.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackBGRTAnimated.WinUI.Views;

public sealed partial class OobePage : Page
{
    private const int TotalSteps = 7;
    private readonly App _app;
    private readonly List<ThemeInfo> _themes = new();
    private int _step = 1;

    public OobePage()
    {
        InitializeComponent();
        _app = (App)Application.Current;

        var settings = _app.Services.Settings.Load();
        DisplayNameBox.Text = settings.DisplayName;

        RefreshSafety();
        RefreshThemes();
        UpdateStepUi();
    }

    private void UpdateStepUi()
    {
        StepWelcomePanel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        StepDisplayNamePanel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        StepSafetyPanel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        StepInstallPanel.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;
        StepThemePanel.Visibility = _step == 5 ? Visibility.Visible : Visibility.Collapsed;
        StepBootPanel.Visibility = _step == 6 ? Visibility.Visible : Visibility.Collapsed;
        StepFinishPanel.Visibility = _step == 7 ? Visibility.Visible : Visibility.Collapsed;

        BackButton.IsEnabled = _step > 1;
        NextButton.Visibility = _step < TotalSteps ? Visibility.Visible : Visibility.Collapsed;
        FinishButton.Visibility = _step == TotalSteps ? Visibility.Visible : Visibility.Collapsed;

        FooterText.Text = $"Step {_step} of {TotalSteps}";

        switch (_step)
        {
            case 1:
                StepHeaderText.Text = "Welcome";
                StepDescriptionText.Text = "Quick guided setup for HackBGRT-Animated.";
                break;
            case 2:
                StepHeaderText.Text = "Display Name";
                StepDescriptionText.Text = "Stored locally in AppData only.";
                break;
            case 3:
                StepHeaderText.Text = "Safety Check";
                StepDescriptionText.Text = "Admin, EFI, and boot-entry visibility checks.";
                break;
            case 4:
                StepHeaderText.Text = "Install or Repair";
                StepDescriptionText.Text = "Creates or repairs the HackBGRT-Animated install and boot entry.";
                break;
            case 5:
                StepHeaderText.Text = "Choose Theme";
                StepDescriptionText.Text = "Select the first active theme. You can import GIF themes afterward.";
                break;
            case 6:
                StepHeaderText.Text = "Boot Behavior";
                StepDescriptionText.Text = "Choose how boot order should be handled right now.";
                break;
            case 7:
                StepHeaderText.Text = "Finish";
                StepDescriptionText.Text = "Review summary, then open Dashboard.";
                SummaryBox.Text = BuildSummary();
                break;
        }
    }

    private void NextButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_step == 2)
        {
            SaveDisplayName();
        }

        if (_step < TotalSteps)
        {
            _step++;
            UpdateStepUi();
            if (_step == 7)
            {
                SummaryBox.Text = BuildSummary();
            }
        }
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_step > 1)
        {
            _step--;
            UpdateStepUi();
        }
    }

    private async void FinishButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!await ApplyBootChoiceAsync())
            {
                return;
            }

            MarkFirstRunComplete();

            var mainWindow = (Application.Current as App)?.MainAppWindow as MainWindow;
            mainWindow?.ExitOobeToDashboard();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private void SaveDisplayName()
    {
        var settings = _app.Services.Settings.Load();
        settings.DisplayName = (DisplayNameBox.Text ?? string.Empty).Trim();
        settings.LastCompletedSetupStep = "display-name";
        _app.Services.Settings.Save(settings);
    }

    private void RefreshSafety()
    {
        var status = _app.Services.Status.GetStatus();
        SafetyAdminText.Text = $"Admin: {(status.IsAdmin ? "Yes" : "No")}";
        SafetyEfiText.Text = $"EFI access: {(status.EfiFolderFound || status.Installed ? "Detected" : "Not detected yet")}";
        SafetyWindowsText.Text = $"Windows Boot Manager found: {(status.WindowsBootManagerFound ? "Yes" : "No")}";
        SafetyHackBgrtText.Text = $"Normal HackBGRT found: {(status.NormalHackBgrtFound ? "Yes" : "No")}";
        SafetyAnimatedText.Text = $"HackBGRT-Animated install/entry: {(status.Installed ? "Present" : "Not installed")}";
    }

    private void RefreshThemes()
    {
        ThemeCombo.Items.Clear();
        _themes.Clear();
        _themes.AddRange(_app.Services.Themes.GetThemes());

        foreach (var theme in _themes)
        {
            ThemeCombo.Items.Add($"{theme.Name} ({theme.AnimationFps} fps / {theme.AnimationMaxMs} ms)");
        }

        var active = _app.Services.Themes.GetActiveTheme();
        var activeIndex = _themes.FindIndex(t => string.Equals(t.Name, active, StringComparison.OrdinalIgnoreCase));
        if (activeIndex >= 0)
        {
            ThemeCombo.SelectedIndex = activeIndex;
        }
        else if (_themes.Count > 0)
        {
            ThemeCombo.SelectedIndex = 0;
        }

        ThemeStatusText.Text = _themes.Count == 0
            ? "No themes found in AppData yet. You can import one from the Import GIF page after setup."
            : $"{_themes.Count} theme(s) available.";
    }

    private async void InstallRepairButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            InstallRepairButton.IsEnabled = false;
            InstallStatusText.Text = "Installing/repairing...";
            await Task.Run(() => _app.Services.Operations.InstallOrUpdate());
            InstallStatusText.Text = "Install/repair completed.";
            RefreshSafety();
        }
        catch (Exception ex)
        {
            InstallStatusText.Text = "Install/repair failed.";
            await ShowErrorAsync(ex.Message);
        }
        finally
        {
            InstallRepairButton.IsEnabled = true;
        }
    }

    private void RefreshSafetyButton_OnClick(object sender, RoutedEventArgs e) => RefreshSafety();

    private async void SetActiveThemeButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ThemeCombo.SelectedIndex < 0 || ThemeCombo.SelectedIndex >= _themes.Count)
            {
                ThemeStatusText.Text = "Select a theme first.";
                return;
            }

            var selectedTheme = _themes[ThemeCombo.SelectedIndex];
            _app.Services.Themes.SetActiveTheme(selectedTheme.Name);
            ThemeStatusText.Text = $"Active theme set to '{selectedTheme.Name}'.";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private void RefreshThemesButton_OnClick(object sender, RoutedEventArgs e) => RefreshThemes();

    private async Task<bool> ApplyBootChoiceAsync()
    {
        if (BootAnimatedFirstRadio.IsChecked == true)
        {
            if (!await ConfirmAsync("Put HackBGRT-Animated First", "Move HackBGRT-Animated to first position?"))
            {
                return false;
            }

            _app.Services.Operations.MakeAnimatedDefault();
            return true;
        }

        if (BootWindowsFirstRadio.IsChecked == true)
        {
            _app.Services.Operations.RestoreWindowsFirst();
        }

        return true;
    }

    private void MarkFirstRunComplete()
    {
        var settings = _app.Services.Settings.Load();
        settings.FirstRunComplete = true;
        settings.LastCompletedSetupStep = "oobe-finished";
        settings.UiPreferences["oobeCompletedAt"] = DateTimeOffset.UtcNow.ToString("O");
        _app.Services.Settings.Save(settings);
    }

    private string BuildSummary()
    {
        var status = _app.Services.Status.GetStatus();
        var settings = _app.Services.Settings.Load();

        var sb = new StringBuilder();
        sb.AppendLine("HackBGRT-Animated first-run summary");
        sb.AppendLine($"Display name: {(string.IsNullOrWhiteSpace(settings.DisplayName) ? "(not set)" : settings.DisplayName)}");
        sb.AppendLine($"Installed: {(status.Installed ? "Yes" : "No")}");
        sb.AppendLine($"Boot entry: {(status.BootEntryFound ? "Found" : "Missing")}");
        sb.AppendLine($"In boot order: {(status.InBootOrder ? "Yes" : "No")}");
        sb.AppendLine($"Default boot option: {(status.IsDefault ? "Yes" : "No")}");
        sb.AppendLine($"Active theme: {settings.ActiveTheme}");
        sb.AppendLine($"AppData: {status.AppDataRoot}");
        return sb.ToString();
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
