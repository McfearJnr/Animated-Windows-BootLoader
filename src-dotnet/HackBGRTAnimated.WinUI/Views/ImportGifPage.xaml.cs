using System.Diagnostics;
using HackBGRTAnimated.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.UI.Xaml.Media;

namespace HackBGRTAnimated.WinUI.Views;

public sealed partial class ImportGifPage : Page
{
    private readonly App _app;

    public ImportGifPage()
    {
        InitializeComponent();
        _app = (App)Application.Current;
    }

    private async void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
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

        SetGifPath(file.Path);
    }

    private void SetGifPath(string path)
    {
        GifPathBox.Text = path;
        if (string.IsNullOrWhiteSpace(ThemeNameBox.Text))
        {
            ThemeNameBox.Text = Path.GetFileNameWithoutExtension(path);
        }

        try
        {
            GifPreview.Source = new BitmapImage(new Uri(path));
        }
        catch
        {
            GifPreview.Source = null;
        }
    }

    private async void Import_OnClick(object sender, RoutedEventArgs e)
    {
        var options = new GifImportOptions
        {
            GifPath = GifPathBox.Text.Trim(),
            ThemeName = ThemeNameBox.Text.Trim(),
            Width = ParseInt(WidthBox.Text, 400),
            Height = ParseInt(HeightBox.Text, 400),
            Fps = ParseInt(FpsBox.Text, 15),
            MaxDurationMs = ParseInt(DurationBox.Text, 3000),
            BackgroundHex = BackgroundBox.Text.Trim(),
            SetActiveAfterImport = SetActiveCheck.IsChecked == true,
        };

        ImportButton.IsEnabled = false;
        ImportProgressBar.Visibility = Visibility.Visible;
        ProgressLog.Text = string.Empty;

        try
        {
            var result = await Task.Run(() => _app.Services.GifImporter.ImportGif(options, ReportProgress));
            ReportProgress($"Imported theme '{result.ThemeName}' ({result.OutputFrameCount} frames, ~{result.EstimatedBytes / 1024} KB).");
            await ShowMessageAsync("GIF import completed in AppData. Run Install / Update to apply to EFI.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
            ReportProgress("ERROR: " + ex.Message);
        }
        finally
        {
            ImportProgressBar.Visibility = Visibility.Collapsed;
            ImportButton.IsEnabled = true;
        }
    }

    private void ReportProgress(string line)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ProgressLog.Text += line + Environment.NewLine;
            ProgressLog.Select(ProgressLog.Text.Length, 0);
        });
    }

    private void OpenThemesFolder_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_app.Services.Paths.ThemesDir}\"") { UseShellExecute = true });
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            DropZoneBorder.BorderBrush = (Brush)Application.Current.Resources["AppSecondaryBrush"];
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
            DropZoneBorder.BorderBrush = (Brush)Application.Current.Resources["AppStrokeBrush"];
        }
    }

    private void Page_DragLeave(object sender, DragEventArgs e)
    {
        DropZoneBorder.BorderBrush = (Brush)Application.Current.Resources["AppStrokeBrush"];
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        var file = items.OfType<StorageFile>().FirstOrDefault(f => f.FileType.Equals(".gif", StringComparison.OrdinalIgnoreCase));
        if (file is not null)
        {
            SetGifPath(file.Path);
        }

        DropZoneBorder.BorderBrush = (Brush)Application.Current.Resources["AppStrokeBrush"];
    }

    private static int ParseInt(string raw, int fallback)
    {
        return int.TryParse(raw, out var parsed) ? parsed : fallback;
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
