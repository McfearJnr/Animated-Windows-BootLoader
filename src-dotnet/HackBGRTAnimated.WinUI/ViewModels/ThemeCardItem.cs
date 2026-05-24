using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml;

namespace HackBGRTAnimated.WinUI.ViewModels;

public sealed class ThemeCardItem
{
    public string Name { get; set; } = string.Empty;
    public int Fps { get; set; }
    public int DurationMs { get; set; }
    public int FrameCount { get; set; }
    public long SizeKb { get; set; }
    public bool IsActive { get; set; }
    public string ThemeDirectory { get; set; } = string.Empty;
    public string SplashPath { get; set; } = string.Empty;
    public BitmapImage? PreviewImage { get; set; }
    public string FpsText => $"FPS: {Fps}";
    public string DurationText => $"Duration: {DurationMs} ms";
    public string TimingText => $"{FpsText} • {DurationText}";
    public string FrameCountText => $"Frames: {FrameCount}";
    public string SizeText => $"Size: {SizeKb} KB";
    public Visibility ActiveBadgeVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
}
