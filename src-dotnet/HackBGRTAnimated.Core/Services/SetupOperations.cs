using System.Diagnostics;
using System.IO.Compression;
using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.Core.Services;

public sealed class SetupOperations
{
    private readonly AppDataPathProvider _paths;
    private readonly EfiVolume _efiVolume;
    private readonly BootEntryManager _bootEntries;
    private readonly SettingsService _settings;
    private readonly ThemeManager _themes;

    public SetupOperations(AppDataPathProvider paths, SettingsService? settings = null, ThemeManager? themeManager = null)
    {
        _paths = paths;
        _settings = settings ?? new SettingsService(paths);
        _themes = themeManager ?? new ThemeManager(paths, _settings);
        _efiVolume = new EfiVolume(paths);
        _bootEntries = new BootEntryManager(paths);
    }

    public void InstallOrUpdate()
    {
        EnsureLocalRuntimeDefaults();

        var esp = RequireEsp();
        var installPath = _efiVolume.GetAnimatedInstallPath(esp);
        Directory.CreateDirectory(installPath);

        CopyLoader(installPath);
        SyncAnimatedConfig(esp, installPath);
        _bootEntries.EnsureAnimatedEntry(esp, makeDefault: false);
        LogAction("Install/Update completed.");
    }

    public void RepairBootEntry()
    {
        var esp = RequireEsp();
        _bootEntries.EnsureAnimatedEntry(esp, makeDefault: false);
        LogAction("Boot entry repair completed.");
    }

    public void SyncAnimatedConfig()
    {
        EnsureLocalRuntimeDefaults();

        var esp = RequireEsp();
        var installPath = _efiVolume.GetAnimatedInstallPath(esp);
        if (!Directory.Exists(installPath))
        {
            throw new InvalidOperationException($"HackBGRT-Animated is not installed at {installPath}");
        }

        SyncAnimatedConfig(esp, installPath);
        LogAction("Config sync to EFI completed.");
    }

    public void MakeAnimatedDefault()
    {
        _bootEntries.PutAnimatedFirst();
        LogAction("Moved HackBGRT-Animated first in boot order.");
    }

    public void RestoreWindowsFirst()
    {
        _bootEntries.RestoreWindowsFirst();
        LogAction("Moved Windows Boot Manager first in boot order.");
    }

    public void DisableAnimated()
    {
        _bootEntries.RemoveAnimatedFromDisplayOrder();
        _bootEntries.RestoreWindowsFirst();
        LogAction("Disabled HackBGRT-Animated in active boot order.");
    }

    public void UninstallAnimated()
    {
        _bootEntries.DeleteAnimatedEntries();
        var esp = RequireEsp();
        var folder = _efiVolume.GetAnimatedInstallPath(esp);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }

        _bootEntries.RestoreWindowsFirst();
        LogAction("Uninstalled HackBGRT-Animated EFI files and boot entries.");
    }

    public void RestartPc()
    {
        var result = ProcessRunner.Run("shutdown", "-f -r -t 1", _paths.RepoRoot);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.CombinedOutput);
        }
    }

    public void RestartAsAdministrator()
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("Cannot resolve current executable.");
        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = _paths.RepoRoot,
        };
        Process.Start(start);
    }

    public string ExportTheme(string themeName, string? destinationDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            throw new InvalidOperationException("Theme name is required.");
        }

        var source = _paths.GetThemeDirectory(themeName);
        if (!Directory.Exists(source))
        {
            throw new InvalidOperationException($"Theme '{themeName}' not found.");
        }

        var destDir = string.IsNullOrWhiteSpace(destinationDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : destinationDirectory;
        Directory.CreateDirectory(destDir);

        var zipPath = Path.Combine(destDir, $"{themeName}.hbgra-theme.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(source, zipPath, CompressionLevel.Optimal, includeBaseDirectory: true);
        LogAction($"Exported theme '{themeName}' to {zipPath}");
        return zipPath;
    }

    public string ImportThemePackage(string packagePath, bool setActive)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Theme package not found.", packagePath);
        }

        var tempRoot = Path.Combine(_paths.CacheDir, "theme-import", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            ZipFile.ExtractToDirectory(packagePath, tempRoot);
            var candidates = Directory.GetDirectories(tempRoot);
            if (candidates.Length == 0)
            {
                throw new InvalidOperationException("Theme package is empty.");
            }

            var themeSource = candidates[0];
            var themeName = Path.GetFileName(themeSource);
            if (string.IsNullOrWhiteSpace(themeName))
            {
                throw new InvalidOperationException("Could not determine theme name from package.");
            }

            var themeTarget = _paths.GetThemeDirectory(themeName);
            if (Directory.Exists(themeTarget))
            {
                Directory.Delete(themeTarget, true);
            }

            CopyDirectoryRecursive(themeSource, themeTarget);

            if (!File.Exists(Path.Combine(themeTarget, "theme.ini")))
            {
                throw new InvalidOperationException("Invalid theme package: missing theme.ini.");
            }

            _themes.EnsureThemeDefaults(themeName);
            if (setActive)
            {
                _themes.SetActiveTheme(themeName);
            }

            LogAction($"Imported theme package '{packagePath}' as '{themeName}'.");
            return themeName;
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    public void ImportSampleTheme(string sampleThemeName, bool setActive)
    {
        var source = Path.Combine(_paths.RepoSamplesThemesDir, sampleThemeName);
        if (!Directory.Exists(source))
        {
            throw new InvalidOperationException($"Sample theme '{sampleThemeName}' not found.");
        }

        var target = _paths.GetThemeDirectory(sampleThemeName);
        if (Directory.Exists(target))
        {
            throw new InvalidOperationException($"Theme '{sampleThemeName}' already exists in AppData.");
        }

        CopyDirectoryRecursive(source, target);
        _themes.EnsureThemeDefaults(sampleThemeName);
        if (setActive)
        {
            _themes.SetActiveTheme(sampleThemeName);
        }

        LogAction($"Imported sample theme '{sampleThemeName}'.");
    }

    private string RequireEsp()
    {
        if (!AppDataPathProvider.IsAdministrator())
        {
            throw new InvalidOperationException("Administrator rights are required for EFI and boot entry operations.");
        }

        var esp = _efiVolume.LocateEspRoot();
        if (string.IsNullOrWhiteSpace(esp))
        {
            throw new InvalidOperationException("EFI System Partition not found.");
        }

        return esp;
    }

    private void CopyLoader(string installPath)
    {
        var signed = Path.Combine(_paths.RepoRoot, "efi-signed", "bootx64.efi");
        var unsigned = Path.Combine(_paths.RepoRoot, "efi", "bootx64.efi");
        var source = File.Exists(signed) ? signed : unsigned;
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("bootx64.efi not found. Build EFI first.", source);
        }

        File.Copy(source, Path.Combine(installPath, "bootx64.efi"), true);
    }

    private void SyncAnimatedConfig(string espRoot, string installPath)
    {
        EnsureLocalRuntimeDefaults();

        var configLines = LoadAppConfigLines();
        ApplyConfigOverrides(configLines);
        File.WriteAllLines(Path.Combine(installPath, "config.txt"), configLines);

        if (File.Exists(_paths.AppSplashPath))
        {
            File.Copy(_paths.AppSplashPath, Path.Combine(installPath, "splash.bmp"), true);
        }

        var themesDest = Path.Combine(installPath, "themes");
        if (Directory.Exists(themesDest))
        {
            Directory.Delete(themesDest, true);
        }

        if (Directory.Exists(_paths.ThemesDir))
        {
            foreach (var theme in _themes.GetThemes())
            {
                _themes.EnsureThemeDefaults(theme.Name);
            }
            CopyDirectoryRecursive(_paths.ThemesDir, themesDest);
        }

        if (!string.IsNullOrWhiteSpace(espRoot))
        {
            LogAction($"Synced AppData configuration to {installPath}");
        }
    }

    private List<string> LoadAppConfigLines()
    {
        if (File.Exists(_paths.AppConfigPath))
        {
            return File.ReadAllLines(_paths.AppConfigPath).ToList();
        }

        if (File.Exists(_paths.RepoConfigTemplatePath))
        {
            return File.ReadAllLines(_paths.RepoConfigTemplatePath).ToList();
        }

        return new List<string>();
    }

    private void ApplyConfigOverrides(List<string> lines)
    {
        var cfg = new ConfigEditor(_paths.AppConfigPath);
        var activeTheme = _settings.GetActiveTheme();
        if (!string.IsNullOrWhiteSpace(activeTheme) && !string.Equals(activeTheme, "none", StringComparison.OrdinalIgnoreCase))
        {
            SetKey(lines, "active_theme", activeTheme);
        }

        var (panicKey, panicEnabled) = _settings.GetPanicKey();
        SetKey(lines, "panic_key_enabled", panicEnabled ? "1" : "0");
        SetKey(lines, "panic_key", panicEnabled ? panicKey : "none");

        // Root fallback values remain valid if a theme is broken.
        SetKey(lines, "animation_fps", cfg.Get("animation_fps", "15") ?? "15");
        SetKey(lines, "animation_max_ms", cfg.Get("animation_max_ms", "3000") ?? "3000");
        SetKey(lines, "animation_path", "\\EFI\\HackBGRT-Animated\\animation\\");

        var splashValue = cfg.Get("image", null);
        if (string.IsNullOrWhiteSpace(splashValue) || splashValue.Contains("\\EFI\\HackBGRT\\", StringComparison.OrdinalIgnoreCase))
        {
            splashValue = "path=\\EFI\\HackBGRT-Animated\\splash.bmp";
        }
        SetKey(lines, "image", splashValue);
    }

    private void EnsureLocalRuntimeDefaults()
    {
        _paths.EnsureDirectories();

        if (!File.Exists(_paths.AppConfigPath))
        {
            if (File.Exists(_paths.RepoConfigTemplatePath))
            {
                File.Copy(_paths.RepoConfigTemplatePath, _paths.AppConfigPath, false);
            }
            else
            {
                File.WriteAllLines(_paths.AppConfigPath,
                [
                    "animation=0",
                    "animation_fps=15",
                    "animation_max_ms=3000",
                    "image=path=\\EFI\\HackBGRT-Animated\\splash.bmp",
                ]);
            }
        }

        if (!File.Exists(_paths.AppSplashPath) && File.Exists(_paths.RepoSplashTemplatePath))
        {
            File.Copy(_paths.RepoSplashTemplatePath, _paths.AppSplashPath, false);
        }
    }

    private static void SetKey(List<string> lines, string key, string value)
    {
        var prefix = key + "=";
        lines.RemoveAll(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        lines.Add(prefix + value);
    }

    private void LogAction(string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.LastActionLogPath) ?? _paths.LogsDir);
        File.AppendAllText(_paths.LastActionLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var target = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, target, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectoryRecursive(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }
}
