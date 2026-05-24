using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using HackBGRTAnimated.Core.Models;
using HackBGRTAnimated.UI.Services;

namespace HackBGRTAnimated.UI.Views;

public partial class ImportGifPage : UserControl
{
    private readonly AppServices _services;
    private string? _lastThemeDir;

    public ImportGifPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
    }

    private void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "GIF files (*.gif)|*.gif|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() == true)
        {
            GifPathBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(ThemeNameBox.Text))
            {
                ThemeNameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void Import_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var options = new GifImportOptions
            {
                GifPath = GifPathBox.Text.Trim(),
                ThemeName = ThemeNameBox.Text.Trim(),
                Width = ParseInt(WidthBox.Text, 400),
                Height = ParseInt(HeightBox.Text, 400),
                Fps = ParseInt(FpsBox.Text, 15),
                MaxDurationMs = ParseInt(MaxMsBox.Text, 3000),
                BackgroundHex = BackgroundBox.Text.Trim(),
                SetActiveAfterImport = SetActiveCheck.IsChecked == true,
            };

            ProgressBox.Clear();
            var result = _services.GifImporter.ImportGif(options, LogProgress);
            _lastThemeDir = result.ThemeDirectory;
            LogProgress($"Imported theme '{result.ThemeName}' ({result.OutputFrameCount} frames, ~{result.EstimatedBytes / 1024} KB).");
            MessageBox.Show("Import completed in AppData. Run Install / Update to apply to EFI.", "HackBGRT-Animated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Error);
            LogProgress("ERROR: " + ex.Message);
        }
    }

    private void Preview_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastThemeDir) || !Directory.Exists(_lastThemeDir))
        {
            MessageBox.Show("No imported theme to preview yet.", "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_lastThemeDir}\"") { UseShellExecute = true });
    }

    private void OnDropGif(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var gif = files.FirstOrDefault(f => f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase));
        if (gif != null)
        {
            GifPathBox.Text = gif;
            if (string.IsNullOrWhiteSpace(ThemeNameBox.Text))
            {
                ThemeNameBox.Text = Path.GetFileNameWithoutExtension(gif);
            }
        }
    }

    private void LogProgress(string line)
    {
        ProgressBox.AppendText(line + Environment.NewLine);
        ProgressBox.ScrollToEnd();
    }

    private static int ParseInt(string raw, int fallback)
    {
        return int.TryParse(raw, out var value) ? value : fallback;
    }
}
