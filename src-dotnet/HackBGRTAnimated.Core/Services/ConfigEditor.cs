namespace HackBGRTAnimated.Core.Services;

public sealed class ConfigEditor
{
    private readonly string _path;
    private readonly List<string> _lines;

    public ConfigEditor(string path)
    {
        _path = path;
        _lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
    }

    public string? Get(string key, string? fallback = null)
    {
        var prefix = key + "=";
        var value = _lines.Where(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(l => l[prefix.Length..]).LastOrDefault();
        return value ?? fallback;
    }

    public void Set(string key, string value)
    {
        var prefix = key + "=";
        _lines.RemoveAll(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        _lines.Add($"{key}={value}");
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? Environment.CurrentDirectory);
        File.WriteAllLines(_path, _lines);
    }
}
