using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingImage = System.Drawing.Image;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace HackBGRTAnimated.WinUI.Views;

public sealed partial class ConfigPage : Page
{
    private readonly App _app;

    public ConfigPage()
    {
        InitializeComponent();
        _app = (App)Application.Current;
        RefreshConfig();
    }

    private void RefreshConfig()
    {
        var activeTheme = _app.Services.Themes.GetActiveTheme();
        ActiveThemeText.Text = $"Editing active theme: {activeTheme}";

        var theme = _app.Services.Themes.GetTheme(activeTheme);
        if (theme is not null)
        {
            FpsBox.Text = theme.AnimationFps.ToString();
            DurationBox.Text = theme.AnimationMaxMs.ToString();
            PreloadToggle.IsOn = theme.AnimationPreload;
            ClearEachFrameToggle.IsOn = theme.AnimationClearEachFrame;
        }

        var (panicKey, panicEnabled) = _app.Services.Themes.GetPanicKey();
        if (!panicEnabled)
        {
            panicKey = "none";
        }

        foreach (var obj in PanicKeyCombo.Items)
        {
            if (obj is ComboBoxItem item && string.Equals(item.Content?.ToString(), panicKey, StringComparison.OrdinalIgnoreCase))
            {
                PanicKeyCombo.SelectedItem = item;
                break;
            }
        }

        if (PanicKeyCombo.SelectedItem is null)
        {
            PanicKeyCombo.SelectedIndex = 0;
        }
    }

    private async void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var activeTheme = _app.Services.Themes.GetActiveTheme();
        if (string.Equals(activeTheme, "none", StringComparison.OrdinalIgnoreCase))
        {
            await ShowErrorAsync("No active theme selected.");
            return;
        }

        if (!int.TryParse(FpsBox.Text, out var fps)) fps = 15;
        if (!int.TryParse(DurationBox.Text, out var maxMs)) maxMs = 3000;

        try
        {
            _app.Services.Themes.SaveThemeSettings(activeTheme, fps, maxMs, PreloadToggle.IsOn, ClearEachFrameToggle.IsOn);
            var panicKey = (PanicKeyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "esc";
            _app.Services.Themes.SavePanicKey(panicKey);
            await ShowMessageAsync("Settings saved in AppData. Run Install / Update to apply to EFI.");
            RefreshConfig();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void SetFallbackImage_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".gif");

        if (_app.MainAppWindow is null)
        {
            await ShowErrorAsync("Main window handle is not available.");
            return;
        }

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_app.MainAppWindow));
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            using var source = DrawingImage.FromFile(file.Path);
            using var bmp = new DrawingBitmap(source.Width, source.Height, DrawingPixelFormat.Format24bppRgb);
            using (var g = DrawingGraphics.FromImage(bmp))
            {
                g.DrawImageUnscaledAndClipped(source, new DrawingRectangle(DrawingPoint.Empty, source.Size));
            }

            bmp.Save(_app.Services.Paths.AppSplashPath, DrawingImageFormat.Bmp);
            await ShowMessageAsync("Fallback splash image updated in AppData. Run Install / Update to apply to EFI.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private void OpenAppData_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_app.Services.Paths.AppDataRoot}\"") { UseShellExecute = true });
    }

    private async void OpenEfi_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var efi = new HackBGRTAnimated.Core.Services.EfiVolume(_app.Services.Paths);
            var esp = efi.LocateEspRoot();
            if (string.IsNullOrWhiteSpace(esp))
            {
                await ShowErrorAsync("EFI System Partition not found.");
                return;
            }

            var path = efi.GetAnimatedInstallPath(esp);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshConfig();

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
