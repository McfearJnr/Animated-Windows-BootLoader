using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HackBGRTAnimated.WinUI.Controls;

public sealed partial class ThemeCard : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ThemeCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(ThemeCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TimingProperty = DependencyProperty.Register(
        nameof(Timing), typeof(string), typeof(ThemeCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatsProperty = DependencyProperty.Register(
        nameof(Stats), typeof(string), typeof(ThemeCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PreviewImageProperty = DependencyProperty.Register(
        nameof(PreviewImage), typeof(ImageSource), typeof(ThemeCard), new PropertyMetadata(null));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(ThemeCard), new PropertyMetadata(false));

    public static readonly DependencyProperty ActiveBadgeVisibilityProperty = DependencyProperty.Register(
        nameof(ActiveBadgeVisibility), typeof(Visibility), typeof(ThemeCard), new PropertyMetadata(Visibility.Collapsed));

    public ThemeCard()
    {
        InitializeComponent();
        ActiveBadgeBackground = (Brush)Application.Current.Resources["AppSuccessBrush"];
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string Timing
    {
        get => (string)GetValue(TimingProperty);
        set => SetValue(TimingProperty, value);
    }

    public string Stats
    {
        get => (string)GetValue(StatsProperty);
        set => SetValue(StatsProperty, value);
    }

    public ImageSource? PreviewImage
    {
        get => (ImageSource?)GetValue(PreviewImageProperty);
        set => SetValue(PreviewImageProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set
        {
            SetValue(IsActiveProperty, value);
            ActiveBadgeVisibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public Brush ActiveBadgeBackground { get; }

    public Visibility ActiveBadgeVisibility
    {
        get => (Visibility)GetValue(ActiveBadgeVisibilityProperty);
        set => SetValue(ActiveBadgeVisibilityProperty, value);
    }

}
