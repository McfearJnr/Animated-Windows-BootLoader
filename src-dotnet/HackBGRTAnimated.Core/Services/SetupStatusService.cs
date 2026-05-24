using HackBGRTAnimated.Core.Models;
using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.Core.Services;

public sealed class SetupStatusService
{
    private readonly AppDataPathProvider _paths;
    private readonly EfiVolume _efiVolume;
    private readonly BootEntryManager _bootEntries;
    private readonly ThemeManager _themes;

    public SetupStatusService(AppDataPathProvider paths, ThemeManager? themeManager = null)
    {
        _paths = paths;
        _efiVolume = new EfiVolume(paths);
        _bootEntries = new BootEntryManager(paths);
        _themes = themeManager ?? new ThemeManager(paths);
    }

    public SetupStatus GetStatus()
    {
        var status = new SetupStatus
        {
            IsAdmin = AppDataPathProvider.IsAdministrator(),
            ActiveTheme = _themes.GetActiveTheme(),
            EfiFolderPath = "\\" + AppDataPathProvider.AnimatedFolderRelative + "\\",
            AppDataRoot = _paths.AppDataRoot,
            AppDataThemesPath = _paths.ThemesDir,
        };

        try
        {
            var esp = _efiVolume.LocateEspRoot();
            if (!string.IsNullOrWhiteSpace(esp))
            {
                var installPath = _efiVolume.GetAnimatedInstallPath(esp);
                status.EfiFolderFound = Directory.Exists(installPath);
                status.EfiFolderPath = installPath;
            }
        }
        catch (Exception ex)
        {
            status.Note = ex.Message;
        }

        try
        {
            var (entries, order, _) = _bootEntries.ReadFirmwareEntries();
            status.BootEntriesReadable = true;
            status.WindowsBootManagerFound = entries.Any(e => string.Equals(e.Description, "Windows Boot Manager", StringComparison.OrdinalIgnoreCase));
            status.NormalHackBgrtFound = entries.Any(e => string.Equals(e.Path, AppDataPathProvider.NormalHackBgrtPath, StringComparison.OrdinalIgnoreCase));
            var animated = entries.FirstOrDefault(e =>
                string.Equals(e.Path, AppDataPathProvider.AnimatedBootPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Description, AppDataPathProvider.AnimatedBootName, StringComparison.OrdinalIgnoreCase));
            status.BootEntryFound = animated is not null;
            if (animated is not null)
            {
                var pos = order.FindIndex(o => string.Equals(o, animated.Identifier, StringComparison.OrdinalIgnoreCase));
                status.InBootOrder = pos >= 0;
                status.IsDefault = pos == 0;
            }
        }
        catch (Exception ex)
        {
            status.BootEntriesReadable = false;
            status.Note = string.IsNullOrWhiteSpace(status.Note) ? ex.Message : status.Note;
        }

        status.Installed = status.EfiFolderFound || status.BootEntryFound;
        return status;
    }
}
