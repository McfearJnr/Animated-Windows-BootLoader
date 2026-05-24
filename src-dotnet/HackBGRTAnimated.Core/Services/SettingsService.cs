using System.Text.Json;
using HackBGRTAnimated.Core.Models;
using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.Core.Services;

public sealed class SettingsService
{
    private readonly AppDataPathProvider _paths;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsService(AppDataPathProvider paths)
    {
        _paths = paths;
        EnsureFiles();
    }

    public UserSettings Load()
    {
        try
        {
            var json = File.ReadAllText(_paths.SettingsPath);
            var loaded = JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions);
            return loaded ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_paths.SettingsPath, json);
    }

    public string GetActiveTheme() => Load().ActiveTheme;

    public void SetActiveTheme(string theme)
    {
        var settings = Load();
        settings.ActiveTheme = theme;
        settings.FirstRunComplete = true;
        Save(settings);
    }

    public (string PanicKey, bool PanicEnabled) GetPanicKey()
    {
        var s = Load();
        return (s.PanicKey, s.PanicKeyEnabled);
    }

    public void SetPanicKey(string key)
    {
        var s = Load();
        if (string.Equals(key, "none", StringComparison.OrdinalIgnoreCase))
        {
            s.PanicKeyEnabled = false;
            s.PanicKey = "none";
        }
        else
        {
            s.PanicKeyEnabled = true;
            s.PanicKey = key.ToLowerInvariant();
        }
        Save(s);
    }

    public void AddRecentGif(string path)
    {
        var s = Load();
        s.RecentGifPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        s.RecentGifPaths.Insert(0, path);
        if (s.RecentGifPaths.Count > 20)
        {
            s.RecentGifPaths = s.RecentGifPaths.Take(20).ToList();
        }
        Save(s);
    }

    private void EnsureFiles()
    {
        _paths.EnsureDirectories();
        if (!File.Exists(_paths.SettingsPath))
        {
            Save(new UserSettings());
        }
        if (!File.Exists(_paths.UserProfilePath))
        {
            File.WriteAllText(_paths.UserProfilePath, "{}\n");
        }
    }
}
