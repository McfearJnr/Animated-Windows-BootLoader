using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.Core.Services;

public sealed class RepoDataMigrationService
{
    private readonly AppDataPathProvider _paths;
    private readonly SettingsService _settings;

    public RepoDataMigrationService(AppDataPathProvider paths, SettingsService settings)
    {
        _paths = paths;
        _settings = settings;
    }

    public IReadOnlyList<string> DetectLegacyItems()
    {
        var items = new List<string>();
        var candidates = new[]
        {
            Path.Combine(_paths.RepoRoot, "themes"),
            Path.Combine(_paths.RepoRoot, "animation"),
            Path.Combine(_paths.RepoRoot, "animation-preview"),
            Path.Combine(_paths.RepoRoot, "setup-log.txt"),
            Path.Combine(_paths.RepoRoot, "setup-gui.log"),
            Path.Combine(_paths.RepoRoot, "lastboot.log"),
            Path.Combine(_paths.RepoRoot, "config.txt"),
            Path.Combine(_paths.RepoRoot, "splash.bmp"),
        };

        foreach (var c in candidates)
        {
            if (Directory.Exists(c) || File.Exists(c))
            {
                items.Add(c);
            }
        }
        return items;
    }

    public string Migrate(bool archiveOldData)
    {
        _paths.EnsureDirectories();
        var items = DetectLegacyItems();
        if (items.Count == 0)
        {
            return "No legacy repo-local data found.";
        }

        var summary = new List<string>();

        var repoThemes = Path.Combine(_paths.RepoRoot, "themes");
        if (Directory.Exists(repoThemes))
        {
            CopyDirectoryRecursive(repoThemes, _paths.ThemesDir);
            summary.Add("Migrated repo themes -> AppData themes.");
        }

        var repoAnim = Path.Combine(_paths.RepoRoot, "animation");
        if (Directory.Exists(repoAnim))
        {
            CopyDirectoryRecursive(repoAnim, Path.Combine(_paths.CacheDir, "legacy-animation"));
            summary.Add("Migrated animation frames -> AppData cache.");
        }

        var repoAnimPreview = Path.Combine(_paths.RepoRoot, "animation-preview");
        if (Directory.Exists(repoAnimPreview))
        {
            CopyDirectoryRecursive(repoAnimPreview, Path.Combine(_paths.CacheDir, "legacy-animation-preview"));
            summary.Add("Migrated animation preview frames -> AppData cache.");
        }

        var logCandidates = new[] { "setup-log.txt", "setup-gui.log", "lastboot.log" };
        foreach (var log in logCandidates)
        {
            var src = Path.Combine(_paths.RepoRoot, log);
            if (!File.Exists(src))
            {
                continue;
            }
            var dst = Path.Combine(_paths.LogsDir, log);
            File.Copy(src, dst, true);
            summary.Add($"Migrated {log} -> AppData logs.");
        }

        var repoConfig = Path.Combine(_paths.RepoRoot, "config.txt");
        if (File.Exists(repoConfig))
        {
            if (!File.Exists(_paths.AppConfigPath))
            {
                File.Copy(repoConfig, _paths.AppConfigPath, true);
                summary.Add("Migrated repo config.txt -> AppData config.txt.");
            }

            var cfg = new ConfigEditor(repoConfig);
            var activeTheme = cfg.Get("active_theme", null);
            if (!string.IsNullOrWhiteSpace(activeTheme) && string.Equals(_settings.GetActiveTheme(), "none", StringComparison.OrdinalIgnoreCase))
            {
                _settings.SetActiveTheme(activeTheme);
                summary.Add("Migrated active_theme from repo config into settings.json.");
            }
            var panicEnabled = cfg.Get("panic_key_enabled", null);
            var panic = cfg.Get("panic_key", null);
            if (!string.IsNullOrWhiteSpace(panic))
            {
                _settings.SetPanicKey((panicEnabled == "0") ? "none" : panic);
                summary.Add("Migrated panic key from repo config into settings.json.");
            }
        }

        var repoSplash = Path.Combine(_paths.RepoRoot, "splash.bmp");
        if (File.Exists(repoSplash) && !File.Exists(_paths.AppSplashPath))
        {
            File.Copy(repoSplash, _paths.AppSplashPath, true);
            summary.Add("Migrated repo splash.bmp -> AppData splash.bmp.");
        }

        if (archiveOldData)
        {
            var archiveRoot = Path.Combine(_paths.RepoRoot, ".migrated-local-data", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(archiveRoot);

            foreach (var item in items)
            {
                var name = Path.GetFileName(item.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                var destination = Path.Combine(archiveRoot, name);
                if (Directory.Exists(item))
                {
                    if (Directory.Exists(destination)) Directory.Delete(destination, true);
                    Directory.Move(item, destination);
                }
                else if (File.Exists(item))
                {
                    if (File.Exists(destination)) File.Delete(destination);
                    File.Move(item, destination);
                }
            }
            summary.Add($"Archived old repo-local data under {archiveRoot}");
        }

        return string.Join(Environment.NewLine, summary);
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectoryRecursive(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }
}
