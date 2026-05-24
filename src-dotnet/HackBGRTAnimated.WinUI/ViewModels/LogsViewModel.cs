namespace HackBGRTAnimated.WinUI.ViewModels;

public sealed class LogsViewModel : ObservableObject
{
    private string _selectedLog = "Boot Log";
    public string SelectedLog
    {
        get => _selectedLog;
        set => SetProperty(ref _selectedLog, value);
    }
}
