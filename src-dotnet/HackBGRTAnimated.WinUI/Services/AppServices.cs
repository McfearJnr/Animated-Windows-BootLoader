using HackBGRTAnimated.Core.Services;
using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.WinUI.Services;

public sealed class AppServices
{
    public AppDataPathProvider Paths { get; }
    public SettingsService Settings { get; }
    public ThemeManager Themes { get; }
    public SetupStatusService Status { get; }
    public SetupOperations Operations { get; }
    public BootEntryManager BootEntries { get; }
    public GifThemeImporter GifImporter { get; }
    public LogService Logs { get; }
    public RepoDataMigrationService Migration { get; }

    public AppServices()
    {
        Paths = new AppDataPathProvider(AppContext.BaseDirectory);
        Settings = new SettingsService(Paths);
        Themes = new ThemeManager(Paths, Settings);
        Status = new SetupStatusService(Paths, Themes);
        Operations = new SetupOperations(Paths, Settings, Themes);
        BootEntries = new BootEntryManager(Paths);
        GifImporter = new GifThemeImporter(Paths, Themes, Settings);
        Logs = new LogService(Paths);
        Migration = new RepoDataMigrationService(Paths, Settings);
    }
}
