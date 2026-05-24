using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using HackBGRTAnimated.UI.Services;

namespace HackBGRTAnimated.UI.Views;

public partial class ConfigPage : UserControl
{
    private readonly AppServices _services;

    public ConfigPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        Refresh();
    }

    private void Refresh()
    {
        var active = _services.Themes.GetActiveTheme();
        ActiveThemeText.Text = $"Editing active theme: {active}";
        var theme = _services.Themes.GetTheme(active);
        if (theme != null)
        {
            FpsBox.Text = theme.AnimationFps.ToString();
            MaxMsBox.Text = theme.AnimationMaxMs.ToString();
            PreloadCheck.IsChecked = theme.AnimationPreload;
            ClearCheck.IsChecked = theme.AnimationClearEachFrame;
        }

        var (key, enabled) = _services.Themes.GetPanicKey();
        if (!enabled) key = "none";

        foreach (ComboBoxItem item in PanicKeyCombo.Items)
        {
            if (string.Equals(item.Content?.ToString(), key, StringComparison.OrdinalIgnoreCase))
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

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var active = _services.Themes.GetActiveTheme();
        if (active == "none")
        {
            MessageBox.Show("No active theme selected.", "Config", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(FpsBox.Text, out var fps)) fps = 15;
        if (!int.TryParse(MaxMsBox.Text, out var maxMs)) maxMs = 3000;

        try
        {
            _services.Themes.SaveThemeSettings(active, fps, maxMs, PreloadCheck.IsChecked == true, ClearCheck.IsChecked == true);
            var panic = (PanicKeyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "esc";
            _services.Themes.SavePanicKey(panic);
            MessageBox.Show("Config updated in AppData. Run Install / Update to apply to EFI.", "HackBGRT-Animated", MessageBoxButton.OK, MessageBoxImage.Information);
            Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetSplash_OnClick(object sender, RoutedEventArgs e)
    {
        var active = _services.Themes.GetActiveTheme();
        var theme = _services.Themes.GetTheme(active);
        if (theme == null)
        {
            MessageBox.Show("No active theme selected.", "Config", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new OpenFileDialog { Filter = "Image files|*.bmp;*.png;*.jpg;*.jpeg;*.gif|All files|*.*" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            using var img = System.Drawing.Image.FromFile(dialog.FileName);
            using var bmp = new System.Drawing.Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.DrawImageUnscaledAndClipped(img, new System.Drawing.Rectangle(System.Drawing.Point.Empty, img.Size));
            }

            bmp.Save(theme.SplashPath, System.Drawing.Imaging.ImageFormat.Bmp);
            MessageBox.Show("Splash image updated in AppData. Run Install / Update to apply to EFI.", "HackBGRT-Animated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => Refresh();
}
