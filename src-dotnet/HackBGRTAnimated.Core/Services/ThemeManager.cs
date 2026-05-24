using HackBGRTAnimated.Core.Models;
using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.Core.Services;

public sealed class ThemeManager
{
    private readonly AppDataPathProvider _paths;
    private readonly SettingsService _settings;

    public ThemeManager(AppDataPathProvider paths, SettingsService? settings = null)
    {
        _paths = paths;
        _settings = settings ?? new SettingsService(paths);
    }

    public string GetActiveTheme() => _settings.GetActiveTheme();

    public void SetActiveTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            throw new InvalidOperationException("Theme name is required.");
        }

        var theme = GetTheme(themeName);
        if (theme is null)
        {
            throw new InvalidOperationException($"Theme '{themeName}' not found.");
        }

        _settings.SetActiveTheme(theme.Name);
    }

    public IReadOnlyList<ThemeInfo> GetThemes()
    {
        Directory.CreateDirectory(_paths.ThemesDir);
        var list = new List<ThemeInfo>();
        foreach (var dir in Directory.GetDirectories(_paths.ThemesDir).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(dir);
            var ini = Path.Combine(dir, "theme.ini");
            if (!File.Exists(ini))
            {
                continue;
            }

            var cfg = new ConfigEditor(ini);
            var info = new ThemeInfo
            {
                Name = name,
                ThemeDirectory = dir,
                ThemeIniPath = ini,
                SplashPath = Path.Combine(dir, "splash.bmp"),
                AnimationDirectory = Path.Combine(dir, "animation"),
                AnimationEnabled = cfg.Get("animation", "0") == "1",
                AnimationFps = ResolveFps(cfg),
                AnimationMaxMs = ResolveMaxMs(cfg),
                AnimationPreload = cfg.Get("animation_preload", "1") == "1",
                AnimationClearEachFrame = cfg.Get("animation_clear_each_frame", "1") == "1",
            };

            if (Directory.Exists(info.AnimationDirectory))
            {
                var frames = Directory.GetFiles(info.AnimationDirectory, "*.bmp").OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                info.FrameCount = frames.Length;
                info.EstimatedBytes = frames.Select(f => new FileInfo(f).Length).Sum();
            }

            list.Add(info);
        }

        return list;
    }

    public ThemeInfo? GetTheme(string name) => GetThemes().FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    public void SaveThemeSettings(string themeName, int fps, int maxMs, bool preload, bool clearEachFrame)
    {
        var theme = GetTheme(themeName) ?? throw new InvalidOperationException($"Theme '{themeName}' not found");
        var cfg = new ConfigEditor(theme.ThemeIniPath);
        cfg.Set("animation", "1");
        cfg.Set("animation_fps", ClampInt(fps, 15, 1, 60).ToString());
        cfg.Set("animation_max_ms", ClampInt(maxMs, 3000, 1, 10000).ToString());
        cfg.Set("animation_preload", preload ? "1" : "0");
        cfg.Set("animation_clear_each_frame", clearEachFrame ? "1" : "0");
        cfg.Save();
    }

    public void SavePanicKey(string key) => _settings.SetPanicKey(key);

    public (string PanicKey, bool PanicEnabled) GetPanicKey() => _settings.GetPanicKey();

    public void EnsureThemeStructure(string themeName)
    {
        var dir = _paths.GetThemeDirectory(themeName);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "animation"));
    }

    public void DeleteTheme(string themeName)
    {
        var active = GetActiveTheme();
        if (string.Equals(active, themeName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot delete the active theme.");
        }

        var dir = _paths.GetThemeDirectory(themeName);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    public void EnsureThemeDefaults(string themeName)
    {
        var theme = GetTheme(themeName) ?? throw new InvalidOperationException($"Theme '{themeName}' not found.");
        var cfg = new ConfigEditor(theme.ThemeIniPath);
        var changed = false;

        changed |= Ensure(cfg, "animation", "1");
        changed |= Ensure(cfg, "animation_prefix", "frame_");
        changed |= Ensure(cfg, "animation_digits", "3");
        changed |= Ensure(cfg, "animation_ext", ".bmp");
        changed |= Ensure(cfg, "animation_fps", ResolveFps(cfg).ToString());
        changed |= Ensure(cfg, "animation_max_ms", ResolveMaxMs(cfg).ToString());
        changed |= Ensure(cfg, "animation_final", "last");
        changed |= Ensure(cfg, "animation_preload", cfg.Get("animation_preload", "1") == "1" ? "1" : "0");
        changed |= Ensure(cfg, "animation_clear_each_frame", cfg.Get("animation_clear_each_frame", "1") == "1" ? "1" : "0");
        changed |= Ensure(cfg, "animation_path", $"\\{AppDataPathProvider.AnimatedFolderRelative}\\themes\\{theme.Name}\\animation\\");
        changed |= Ensure(cfg, "image", $"path=\\{AppDataPathProvider.AnimatedFolderRelative}\\themes\\{theme.Name}\\splash.bmp");

        if (changed)
        {
            cfg.Save();
        }
    }

    private int ResolveFps(ConfigEditor cfg)
    {
        var root = new ConfigEditor(_paths.AppConfigPath);
        var rootFps = ClampInt(root.Get("animation_fps", "15"), 15, 1, 60);
        return ClampInt(cfg.Get("animation_fps", rootFps.ToString()), rootFps, 1, 60);
    }

    private int ResolveMaxMs(ConfigEditor cfg)
    {
        var root = new ConfigEditor(_paths.AppConfigPath);
        var rootMax = ClampInt(root.Get("animation_max_ms", "3000"), 3000, 1, 10000);
        return ClampInt(cfg.Get("animation_max_ms", rootMax.ToString()), rootMax, 1, 10000);
    }

    private static bool Ensure(ConfigEditor cfg, string key, string value)
    {
        var current = cfg.Get(key, null);
        if (string.Equals(current, value, StringComparison.Ordinal))
        {
            return false;
        }

        cfg.Set(key, value);
        return true;
    }

    private static int ClampInt(string? value, int fallback, int min, int max)
    {
        if (!int.TryParse(value, out var parsed))
        {
            return fallback;
        }

        return ClampInt(parsed, fallback, min, max);
    }

    private static int ClampInt(int value, int fallback, int min, int max)
    {
        if (value < min || value > max)
        {
            return Math.Min(max, Math.Max(min, fallback));
        }

        return value;
    }
}
