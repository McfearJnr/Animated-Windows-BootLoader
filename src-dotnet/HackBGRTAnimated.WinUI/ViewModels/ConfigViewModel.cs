namespace HackBGRTAnimated.WinUI.ViewModels;

public sealed class ConfigViewModel : ObservableObject
{
    private string _activeTheme = "none";
    public string ActiveTheme
    {
        get => _activeTheme;
        set => SetProperty(ref _activeTheme, value);
    }
}
