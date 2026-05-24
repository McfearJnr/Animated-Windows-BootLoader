using HackBGRTAnimated.Core.Services;
using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.UI.Services;

public sealed class AppServices
{
    public AppDataPathProvider Paths { get; }
    public SettingsService Settings { get; }
    public SetupStatusService Status { get; }
    public ThemeManager Themes { get; }
    public GifThemeImporter GifImporter { get; }
    public BootEntryManager BootEntries { get; }
    public SetupOperations Operations { get; }
    public LogService Logs { get; }
    public RepoDataMigrationService Migration { get; }

    public AppServices()
    {
        Paths = new AppDataPathProvider(AppContext.BaseDirectory);
        Settings = new SettingsService(Paths);
        Themes = new ThemeManager(Paths, Settings);
        Status = new SetupStatusService(Paths, Themes);
        GifImporter = new GifThemeImporter(Paths, Themes, Settings);
        BootEntries = new BootEntryManager(Paths);
        Operations = new SetupOperations(Paths, Settings, Themes);
        Logs = new LogService(Paths);
        Migration = new RepoDataMigrationService(Paths, Settings);
    }
}
