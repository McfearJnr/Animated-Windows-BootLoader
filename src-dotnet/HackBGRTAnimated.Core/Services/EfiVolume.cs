using System.Text.RegularExpressions;
using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.Core.Services;

public sealed class EfiVolume
{
    private readonly AppDataPathProvider _paths;

    public EfiVolume(AppDataPathProvider paths) => _paths = paths;

    public string? LocateEspRoot()
    {
        var output = ProcessRunner.Run("mountvol", "", _paths.RepoRoot).CombinedOutput;
        var match = Regex.Match(output, "EFI[^\n]*\\r?\\n[ \\t]*([A-Z]:\\\\)", RegexOptions.IgnoreCase);
        if (match.Success && HasEfiDirectory(match.Groups[1].Value))
        {
            return match.Groups[1].Value;
        }

        foreach (var letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        {
            var candidate = $"{letter}:\\";
            if (HasEfiDirectory(candidate) && File.Exists(Path.Combine(candidate, AppDataPathProvider.WindowsBootPath.TrimStart('\\').Replace('\\', Path.DirectorySeparatorChar))))
            {
                return candidate;
            }
        }

        var mount = ProcessRunner.Run("mountvol", "S: /S", _paths.RepoRoot);
        if (mount.Success && HasEfiDirectory("S:\\"))
        {
            return "S:\\";
        }

        return null;
    }

    public string GetAnimatedInstallPath(string espRoot)
    {
        return Path.Combine(espRoot, AppDataPathProvider.AnimatedFolderRelative.Replace('\\', Path.DirectorySeparatorChar));
    }

    private static bool HasEfiDirectory(string root)
    {
        try
        {
            return Directory.Exists(Path.Combine(root, "EFI"));
        }
        catch
        {
            return false;
        }
    }
}
