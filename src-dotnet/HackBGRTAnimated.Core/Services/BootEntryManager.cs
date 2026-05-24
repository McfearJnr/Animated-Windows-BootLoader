using System.Text.RegularExpressions;
using HackBGRTAnimated.Core.Models;
using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.Core.Services;

public sealed class BootEntryManager
{
    private readonly AppDataPathProvider _paths;
    private static readonly Regex GuidRegex = new(@"\{[0-9a-fA-F\-]{36}\}", RegexOptions.Compiled);

    public BootEntryManager(AppDataPathProvider paths)
    {
        _paths = paths;
    }

    public (List<BootEntryInfo> Entries, List<string> DisplayOrder, string RawOutput) ReadFirmwareEntries()
    {
        var cmd = ProcessRunner.Run("bcdedit", "/enum firmware /v", _paths.RepoRoot);
        if (!cmd.Success)
        {
            throw new InvalidOperationException($"bcdedit failed: {cmd.CombinedOutput}");
        }

        var raw = cmd.CombinedOutput.Replace("\r", string.Empty);
        var lines = raw.Split('\n');
        var entries = new List<BootEntryInfo>();
        var displayOrder = new List<string>();

        string? sectionId = null;
        BootEntryInfo? current = null;
        var parsingDisplayOrder = false;

        void FlushCurrent()
        {
            if (current is not null && !string.IsNullOrWhiteSpace(current.Identifier))
            {
                entries.Add(current);
            }

            current = null;
        }

        foreach (var lineRaw in lines)
        {
            var line = lineRaw.Trim();
            if (string.IsNullOrEmpty(line))
            {
                FlushCurrent();
                sectionId = null;
                parsingDisplayOrder = false;
                continue;
            }

            var idMatch = Regex.Match(line, "^identifier\\s+(.+)$", RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                FlushCurrent();
                sectionId = idMatch.Groups[1].Value.Trim();
                if (GuidRegex.IsMatch(sectionId))
                {
                    current = new BootEntryInfo { Identifier = sectionId, Description = sectionId };
                }

                continue;
            }

            if (string.Equals(sectionId, "{fwbootmgr}", StringComparison.OrdinalIgnoreCase))
            {
                if (line.StartsWith("displayorder", StringComparison.OrdinalIgnoreCase))
                {
                    parsingDisplayOrder = true;
                }

                if (parsingDisplayOrder)
                {
                    foreach (Match m in GuidRegex.Matches(line))
                    {
                        displayOrder.Add(m.Value);
                    }

                    if (!line.StartsWith("displayorder", StringComparison.OrdinalIgnoreCase) && !GuidRegex.IsMatch(line))
                    {
                        parsingDisplayOrder = false;
                    }
                }

                continue;
            }

            if (current is null)
            {
                continue;
            }

            var descMatch = Regex.Match(line, "^description\\s+(.+)$", RegexOptions.IgnoreCase);
            if (descMatch.Success)
            {
                current.Description = descMatch.Groups[1].Value.Trim();
                continue;
            }

            var pathMatch = Regex.Match(line, "^path\\s+(.+)$", RegexOptions.IgnoreCase);
            if (pathMatch.Success)
            {
                current.Path = pathMatch.Groups[1].Value.Trim();
            }
        }

        FlushCurrent();
        return (entries, displayOrder, raw);
    }

    public BootEntryInfo? FindAnimatedEntry()
    {
        var (entries, _, _) = ReadFirmwareEntries();
        return entries.FirstOrDefault(IsAnimatedEntry);
    }

    public BootEntryInfo? FindWindowsEntry()
    {
        var (entries, _, _) = ReadFirmwareEntries();
        return entries.FirstOrDefault(e =>
            string.Equals(e.Description, "Windows Boot Manager", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Path, AppDataPathProvider.WindowsBootPath, StringComparison.OrdinalIgnoreCase));
    }

    public void SaveDisplayOrder(IReadOnlyList<string> orderedIdentifiers)
    {
        if (orderedIdentifiers.Count == 0)
        {
            throw new InvalidOperationException("Display order cannot be empty");
        }

        var args = "/set {fwbootmgr} displayorder " + string.Join(" ", orderedIdentifiers);
        var result = ProcessRunner.Run("bcdedit", args, _paths.RepoRoot);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.CombinedOutput);
        }
    }

    public void RestoreWindowsFirst()
    {
        var (entries, order, _) = ReadFirmwareEntries();
        var windows = entries.FirstOrDefault(e => string.Equals(e.Description, "Windows Boot Manager", StringComparison.OrdinalIgnoreCase));
        if (windows is null)
        {
            throw new InvalidOperationException("Windows Boot Manager entry not found.");
        }

        var list = order.Where(g => !string.Equals(g, windows.Identifier, StringComparison.OrdinalIgnoreCase)).ToList();
        list.Insert(0, windows.Identifier);
        SaveDisplayOrder(list);
    }

    public void PutAnimatedFirst()
    {
        var (entries, order, _) = ReadFirmwareEntries();
        var animated = entries.FirstOrDefault(IsAnimatedEntry);
        if (animated is null)
        {
            throw new InvalidOperationException("HackBGRT-Animated boot entry not found.");
        }

        var list = order.Where(g => !string.Equals(g, animated.Identifier, StringComparison.OrdinalIgnoreCase)).ToList();
        list.Insert(0, animated.Identifier);
        SaveDisplayOrder(list);
    }

    public void RemoveAnimatedFromDisplayOrder()
    {
        var (entries, order, _) = ReadFirmwareEntries();
        var animated = entries.Where(IsAnimatedEntry).ToList();
        if (!animated.Any())
        {
            return;
        }

        var ids = animated.Select(a => a.Identifier).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newOrder = order.Where(o => !ids.Contains(o)).ToList();
        if (newOrder.Count > 0)
        {
            SaveDisplayOrder(newOrder);
        }
    }

    public void DeleteAnimatedEntries()
    {
        var (entries, _, _) = ReadFirmwareEntries();
        foreach (var entry in entries.Where(IsAnimatedEntry))
        {
            var result = ProcessRunner.Run("bcdedit", $"/delete {entry.Identifier}", _paths.RepoRoot);
            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to delete {entry.Identifier}: {result.CombinedOutput}");
            }
        }
    }

    public void MakeAnimatedDefault()
    {
        var animated = FindAnimatedEntry() ?? throw new InvalidOperationException("HackBGRT-Animated boot entry not found.");
        var result = ProcessRunner.Run("bcdedit", $"/set {{fwbootmgr}} default {animated.Identifier}", _paths.RepoRoot);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.CombinedOutput);
        }
    }

    public void EnsureAnimatedEntry(string espRoot, bool makeDefault = false)
    {
        var partition = espRoot.TrimEnd('\\');
        if (partition.EndsWith(":"))
        {
            partition = partition[..^1];
        }

        if (partition.Length >= 2)
        {
            partition = partition[..2];
        }

        var animated = FindAnimatedEntry();
        string identifier;
        if (animated is null)
        {
            var created = ProcessRunner.Run("bcdedit", $"/create /d \"{AppDataPathProvider.AnimatedBootName}\" /application BOOTAPP", _paths.RepoRoot);
            if (!created.Success)
            {
                throw new InvalidOperationException(created.CombinedOutput);
            }

            var guid = GuidRegex.Match(created.CombinedOutput);
            if (!guid.Success)
            {
                throw new InvalidOperationException("Failed to parse new boot entry GUID.");
            }

            identifier = guid.Value;
        }
        else
        {
            identifier = animated.Identifier;
        }

        var setDevice = ProcessRunner.Run("bcdedit", $"/set {identifier} device partition={partition}", _paths.RepoRoot);
        if (!setDevice.Success)
        {
            throw new InvalidOperationException(setDevice.CombinedOutput);
        }

        var setPath = ProcessRunner.Run("bcdedit", $"/set {identifier} path {AppDataPathProvider.AnimatedBootPath}", _paths.RepoRoot);
        if (!setPath.Success)
        {
            throw new InvalidOperationException(setPath.CombinedOutput);
        }

        var addLast = ProcessRunner.Run("bcdedit", $"/set {{fwbootmgr}} displayorder {identifier} /addlast", _paths.RepoRoot);
        if (!addLast.Success)
        {
            throw new InvalidOperationException(addLast.CombinedOutput);
        }

        if (makeDefault)
        {
            PutAnimatedFirst();
        }
    }

    private static bool IsAnimatedEntry(BootEntryInfo entry)
    {
        return string.Equals(entry.Description, AppDataPathProvider.AnimatedBootName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(entry.Path, AppDataPathProvider.AnimatedBootPath, StringComparison.OrdinalIgnoreCase);
    }
}
