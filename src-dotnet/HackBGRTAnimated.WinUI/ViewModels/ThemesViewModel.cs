using System.Collections.ObjectModel;

namespace HackBGRTAnimated.WinUI.ViewModels;

public sealed class ThemesViewModel : ObservableObject
{
    public ObservableCollection<ThemeCardItem> Themes { get; } = [];

    private string _activeTheme = "none";
    public string ActiveTheme
    {
        get => _activeTheme;
        set => SetProperty(ref _activeTheme, value);
    }
}
