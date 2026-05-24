using System.Collections.ObjectModel;
using System.Diagnostics;
using HackBGRTAnimated.WinUI.Services;
using HackBGRTAnimated.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace HackBGRTAnimated.WinUI.Views;

public sealed partial class ThemesPage : Page
{
    private readonly App _app;
    private readonly ObservableCollection<ThemeCardItem> _themes = [];

    public ThemesPage()
    {
        InitializeComponent();
        _app = (App)Application.Current;
        ThemesList.ItemsSource = _themes;
        if (!UiMotionSettings.AreAnimationsEnabled)
        {
            ThemesList.ItemContainerTransitions = null;
        }
        RefreshThemes();
    }

    private ThemeCardItem? SelectedTheme => ThemesList.SelectedItem as ThemeCardItem;

    private void RefreshThemes()
    {
        _themes.Clear();
        var active = _app.Services.Themes.GetActiveTheme();
        ActiveThemeText.Text = $"Active theme: {active}";

        foreach (var theme in _app.Services.Themes.GetThemes())
        {
            var previewFile = ResolvePreview(theme.SplashPath, theme.AnimationDirectory);
            BitmapImage? previewImage = null;
            if (!string.IsNullOrWhiteSpace(previewFile) && File.Exists(previewFile))
            {
                previewImage = new BitmapImage(new Uri(previewFile));
            }

            _themes.Add(new ThemeCardItem
            {
                Name = theme.Name,
                Fps = theme.AnimationFps,
                DurationMs = theme.AnimationMaxMs,
                FrameCount = theme.FrameCount,
                SizeKb = theme.EstimatedBytes / 1024,
                IsActive = string.Equals(theme.Name, active, StringComparison.OrdinalIgnoreCase),
                ThemeDirectory = theme.ThemeDirectory,
                SplashPath = theme.SplashPath,
                PreviewImage = previewImage,
            });
        }

        ThemeCountText.Text = $"Installed themes: {_themes.Count}";
        ThemesEmptyState.Visibility = _themes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ThemesList.Visibility = _themes.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string? ResolvePreview(string splashPath, string animationDir)
    {
        if (File.Exists(splashPath))
        {
            return splashPath;
        }

        if (!Directory.Exists(animationDir))
        {
            return null;
        }

        return Directory.GetFiles(animationDir, "*.bmp").OrderBy(p => p).FirstOrDefault();
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshThemes();

    private async void SetActive_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedTheme is null)
        {
            await ShowErrorAsync("Select a theme first.");
            return;
        }

        try
        {
            _app.Services.Themes.SetActiveTheme(SelectedTheme.Name);
            RefreshThemes();
            await ShowMessageAsync($"Active theme set to '{SelectedTheme.Name}'. Run Install / Update to apply to EFI.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void EditTiming_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedTheme is null)
        {
            await ShowErrorAsync("Select a theme first.");
            return;
        }

        var fpsBox = new TextBox { Text = SelectedTheme.Fps.ToString() };
        var durationBox = new TextBox { Text = SelectedTheme.DurationMs.ToString() };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Animation FPS (1-60)" });
        panel.Children.Add(fpsBox);
        panel.Children.Add(new TextBlock { Text = "Animation duration ms (1-10000)" });
        panel.Children.Add(durationBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Edit {SelectedTheme.Name}",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (!int.TryParse(fpsBox.Text, out var fps)) fps = 15;
        if (!int.TryParse(durationBox.Text, out var duration)) duration = 3000;

        try
        {
            var theme = _app.Services.Themes.GetTheme(SelectedTheme.Name);
            var preload = theme?.AnimationPreload ?? true;
            var clearEach = theme?.AnimationClearEachFrame ?? true;
            _app.Services.Themes.SaveThemeSettings(SelectedTheme.Name, fps, duration, preload, clearEach);
            RefreshThemes();
            await ShowMessageAsync("Theme timing updated. Run Install / Update to apply to EFI.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void DeleteTheme_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedTheme is null)
        {
            await ShowErrorAsync("Select a theme first.");
            return;
        }

        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Delete Theme",
            Content = $"Delete theme '{SelectedTheme.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _app.Services.Themes.DeleteTheme(SelectedTheme.Name);
            RefreshThemes();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void OpenFolder_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedTheme is null)
        {
            await ShowErrorAsync("Select a theme first.");
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{SelectedTheme.ThemeDirectory}\"") { UseShellExecute = true });
    }

    private async void Preview_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedTheme is null || string.IsNullOrWhiteSpace(SelectedTheme.SplashPath) || !File.Exists(SelectedTheme.SplashPath))
        {
            await ShowErrorAsync("No preview image was found for the selected theme.");
            return;
        }

        var previewImage = new Image
        {
            Source = new BitmapImage(new Uri(SelectedTheme.SplashPath)),
            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
            Height = 320
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = SelectedTheme.Name,
            Content = previewImage,
            CloseButtonText = "Close",
        };

        await dialog.ShowAsync();
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
}
