using HackBGRTAnimated.Core.Models;
using HackBGRTAnimated.WinUI.Services;

namespace HackBGRTAnimated.WinUI.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly AppServices _services;
    private SetupStatus _status = new();
    private string _displayName = string.Empty;

    public DashboardViewModel(AppServices services)
    {
        _services = services;
    }

    public SetupStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string GreetingName => string.IsNullOrWhiteSpace(_displayName) ? "Operator" : _displayName;
    public string Installed => Status.Installed ? "Installed" : "Not installed";
    public string InstalledDetail => Status.Installed ? "Core files and boot entry are detectable." : "Run Install / Update to set up EFI files and entry.";
    public string BootEntry => Status.BootEntryFound ? "Found" : "Missing";
    public string BootEntryDetail => Status.BootEntryFound ? "Firmware entry is visible." : "Boot entry repair is recommended.";
    public string ActiveTheme => string.IsNullOrWhiteSpace(Status.ActiveTheme) ? "none" : Status.ActiveTheme;
    public string ActiveThemeDetail => "Theme stored locally in AppData.";
    public string BootOrder => Status.InBootOrder ? "Included" : "Not in order";
    public string BootOrderDetail => Status.IsDefault ? "Currently first in boot order." : "Selectable from firmware menu.";
    public string AdminState => Status.IsAdmin ? "Elevated" : "Standard user";
    public string AdminDetail => Status.IsAdmin ? "Administrative operations available." : "Install and boot operations require elevation.";
    public string EfiFolder => Status.EfiFolderFound ? "Accessible" : "Unavailable";
    public string EfiFolderDetail => string.IsNullOrWhiteSpace(Status.EfiFolderPath) ? "EFI partition path is not available." : Status.EfiFolderPath;
    public string AppDataRoot => Status.AppDataRoot;

    public void Refresh()
    {
        Status = _services.Status.GetStatus();
        _displayName = _services.Settings.Load().DisplayName;

        OnPropertyChanged(nameof(GreetingName));
        OnPropertyChanged(nameof(Installed));
        OnPropertyChanged(nameof(InstalledDetail));
        OnPropertyChanged(nameof(BootEntry));
        OnPropertyChanged(nameof(BootEntryDetail));
        OnPropertyChanged(nameof(ActiveTheme));
        OnPropertyChanged(nameof(ActiveThemeDetail));
        OnPropertyChanged(nameof(BootOrder));
        OnPropertyChanged(nameof(BootOrderDetail));
        OnPropertyChanged(nameof(AdminState));
        OnPropertyChanged(nameof(AdminDetail));
        OnPropertyChanged(nameof(EfiFolder));
        OnPropertyChanged(nameof(EfiFolderDetail));
        OnPropertyChanged(nameof(AppDataRoot));
    }
}
