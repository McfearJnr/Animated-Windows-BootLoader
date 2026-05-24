using System.Security.Principal;

namespace HackBGRTAnimated.Core.Utilities;

public class AppDataPathProvider
{
    public const string AppName = "HackBGRT-Animated";
    public const string AnimatedFolderRelative = "EFI\\HackBGRT-Animated";
    public const string AnimatedBootPath = "\\EFI\\HackBGRT-Animated\\bootx64.efi";
    public const string AnimatedBootName = "HackBGRT-Animated";
    public const string NormalHackBgrtPath = "\\EFI\\HackBGRT\\loader.efi";
    public const string WindowsBootPath = "\\EFI\\Microsoft\\Boot\\bootmgfw.efi";

    public string RepoRoot { get; }
    public string ExecutableDirectory { get; }
    public bool PortableModeEnabled { get; }

    public string AppDataRoot { get; }
    public string SettingsPath => Path.Combine(AppDataRoot, "settings.json");
    public string UserProfilePath => Path.Combine(AppDataRoot, "user.json");
    public string LogsDir => Path.Combine(AppDataRoot, "logs");
    public string SetupLogPath => Path.Combine(LogsDir, "setup.log");
    public string LastActionLogPath => Path.Combine(LogsDir, "last-action.log");
    public string ThemesDir => Path.Combine(AppDataRoot, "themes");
    public string CacheDir => Path.Combine(AppDataRoot, "cache");
    public string BackupsDir => Path.Combine(AppDataRoot, "backups");
    public string TemplatesDir => Path.Combine(AppDataRoot, "templates");

    // Local editable runtime config staged in AppData and copied to EFI on install/sync.
    public string AppConfigPath => Path.Combine(AppDataRoot, "config.txt");
    public string AppSplashPath => Path.Combine(AppDataRoot, "splash.bmp");

    // Repo-only defaults/samples.
    public string RepoDefaultsDir => Path.Combine(RepoRoot, "defaults");
    public string RepoSamplesThemesDir => Path.Combine(RepoRoot, "samples", "themes");
    public string RepoConfigTemplatePath => Path.Combine(RepoRoot, "config.txt");
    public string RepoSplashTemplatePath => Path.Combine(RepoRoot, "splash.bmp");

    public string SetupExePath => Path.Combine(RepoRoot, "setup.exe");
    public string PortableMarkerPath => Path.Combine(ExecutableDirectory, "portable.mode");

    public AppDataPathProvider(string? baseDirectory = null)
    {
        ExecutableDirectory = baseDirectory ?? AppContext.BaseDirectory;
        RepoRoot = ResolveRepoRoot(ExecutableDirectory);
        PortableModeEnabled = File.Exists(Path.Combine(ExecutableDirectory, "portable.mode"));
        AppDataRoot = PortableModeEnabled
            ? Path.Combine(ExecutableDirectory, "HackBGRT-Animated-Data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

        EnsureDirectories();
        SetupLogger.LogFilePath = SetupLogPath;
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(ThemesDir);
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(BackupsDir);
        Directory.CreateDirectory(TemplatesDir);
    }

    public string GetThemeDirectory(string themeName) => Path.Combine(ThemesDir, themeName);
    public string GetThemeIniPath(string themeName) => Path.Combine(GetThemeDirectory(themeName), "theme.ini");
    public string GetThemeSplashPath(string themeName) => Path.Combine(GetThemeDirectory(themeName), "splash.bmp");
    public string GetThemeAnimationDirectory(string themeName) => Path.Combine(GetThemeDirectory(themeName), "animation");

    public static bool IsAdministrator()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            var hasSetup = File.Exists(Path.Combine(dir.FullName, "setup.exe")) || File.Exists(Path.Combine(dir.FullName, "Makefile"));
            var hasSrc = Directory.Exists(Path.Combine(dir.FullName, "src"));
            if (hasSetup || hasSrc)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return start;
    }
}
