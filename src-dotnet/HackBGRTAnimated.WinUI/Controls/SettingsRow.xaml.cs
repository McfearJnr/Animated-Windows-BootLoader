using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackBGRTAnimated.WinUI.Controls;

public sealed partial class SettingsRow : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingControlProperty = DependencyProperty.Register(
        nameof(SettingControl), typeof(UIElement), typeof(SettingsRow), new PropertyMetadata(null));

    public SettingsRow()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public UIElement? SettingControl
    {
        get => (UIElement?)GetValue(SettingControlProperty);
        set => SetValue(SettingControlProperty, value);
    }
}
