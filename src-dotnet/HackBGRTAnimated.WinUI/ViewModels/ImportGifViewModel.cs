namespace HackBGRTAnimated.WinUI.ViewModels;

public sealed class ImportGifViewModel : ObservableObject
{
    private string _gifPath = string.Empty;
    public string GifPath
    {
        get => _gifPath;
        set => SetProperty(ref _gifPath, value);
    }

    private string _themeName = string.Empty;
    public string ThemeName
    {
        get => _themeName;
        set => SetProperty(ref _themeName, value);
    }
}
