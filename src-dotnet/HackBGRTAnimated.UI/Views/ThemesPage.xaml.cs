using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using HackBGRTAnimated.Core.Models;
using HackBGRTAnimated.UI.Services;

namespace HackBGRTAnimated.UI.Views;

public partial class ThemesPage : UserControl
{
    private readonly AppServices _services;

    public ThemesPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        RefreshThemes();
    }

    private void RefreshThemes()
    {
        var active = _services.Themes.GetActiveTheme();
        ActiveThemeText.Text = $"Active theme: {active}";
        ThemesPathText.Text = $"AppData themes folder: {_services.Paths.ThemesDir}";

        var themes = _services.Themes.GetThemes().Select(t =>
        {
            t.EstimatedBytes /= 1024;
            return t;
        }).ToList();
        ThemeList.ItemsSource = themes;
    }

    private ThemeInfo? SelectedTheme => ThemeList.SelectedItem as ThemeInfo;

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshThemes();

    private void SetActive_OnClick(object sender, RoutedEventArgs e)
    {
        var theme = SelectedTheme;
        if (theme is null)
        {
            return;
        }

        try
        {
            _services.Themes.SetActiveTheme(theme.Name);
            RefreshThemes();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditTiming_OnClick(object sender, RoutedEventArgs e)
    {
        var theme = SelectedTheme;
        if (theme is null)
        {
            return;
        }

        var fps = PromptInt("Animation FPS (1-60)", theme.AnimationFps, 1, 60);
        if (fps is null) return;
        var maxMs = PromptInt("Animation max duration ms (1-10000)", theme.AnimationMaxMs, 1, 10000);
        if (maxMs is null) return;

        try
        {
            _services.Themes.SaveThemeSettings(theme.Name, fps.Value, maxMs.Value, theme.AnimationPreload, theme.AnimationClearEachFrame);
            RefreshThemes();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static int? PromptInt(string title, int current, int min, int max)
    {
        var input = InputDialog.Prompt("Theme setting", title, current.ToString());
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (!int.TryParse(input, out var value) || value < min || value > max)
        {
            MessageBox.Show($"Value must be {min}-{max}.", "Invalid value", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        return value;
    }

    private void DeleteTheme_OnClick(object sender, RoutedEventArgs e)
    {
        var theme = SelectedTheme;
        if (theme is null) return;
        if (MessageBox.Show($"Delete theme '{theme.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _services.Themes.DeleteTheme(theme.Name);
            RefreshThemes();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Preview_OnClick(object sender, RoutedEventArgs e)
    {
        var theme = SelectedTheme;
        if (theme is null) return;
        var first = Directory.Exists(theme.AnimationDirectory)
            ? Directory.GetFiles(theme.AnimationDirectory, "*.bmp").OrderBy(p => p).FirstOrDefault()
            : null;
        if (first is null)
        {
            MessageBox.Show("No preview frame found.", "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo(first) { UseShellExecute = true });
    }

    private void OpenFolder_OnClick(object sender, RoutedEventArgs e)
    {
        var theme = SelectedTheme;
        if (theme is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{theme.ThemeDirectory}\"") { UseShellExecute = true });
    }

    private void OpenThemesFolder_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_services.Paths.ThemesDir}\"") { UseShellExecute = true });
    }

    private void ExportTheme_OnClick(object sender, RoutedEventArgs e)
    {
        var theme = SelectedTheme;
        if (theme is null)
        {
            MessageBox.Show("Select a theme first.", "Export Theme", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var zip = _services.Operations.ExportTheme(theme.Name);
            MessageBox.Show($"Theme exported to:\n{zip}", "Export Theme", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportTheme_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Theme package (*.zip;*.hbgra-theme.zip)|*.zip;*.hbgra-theme.zip|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var setActive = MessageBox.Show("Set imported theme as active?", "Import Theme", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            var themeName = _services.Operations.ImportThemePackage(dialog.FileName, setActive);
            RefreshThemes();
            MessageBox.Show($"Imported theme: {themeName}", "Import Theme", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
