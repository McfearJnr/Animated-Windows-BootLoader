namespace HackBGRTAnimated.Core.Models;

public sealed class SetupStatus
{
    public bool IsAdmin { get; set; }
    public bool Installed { get; set; }
    public bool EfiFolderFound { get; set; }
    public bool BootEntryFound { get; set; }
    public bool InBootOrder { get; set; }
    public bool IsDefault { get; set; }
    public bool WindowsBootManagerFound { get; set; }
    public bool NormalHackBgrtFound { get; set; }
    public bool BootEntriesReadable { get; set; }
    public string ActiveTheme { get; set; } = "none";
    public string EfiFolderPath { get; set; } = string.Empty;
    public string AppDataRoot { get; set; } = string.Empty;
    public string AppDataThemesPath { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed class ThemeInfo
{
    public string Name { get; set; } = string.Empty;
    public string ThemeDirectory { get; set; } = string.Empty;
    public string ThemeIniPath { get; set; } = string.Empty;
    public string AnimationDirectory { get; set; } = string.Empty;
    public string SplashPath { get; set; } = string.Empty;
    public int AnimationFps { get; set; } = 15;
    public int AnimationMaxMs { get; set; } = 3000;
    public bool AnimationEnabled { get; set; }
    public bool AnimationPreload { get; set; } = true;
    public bool AnimationClearEachFrame { get; set; } = true;
    public int FrameCount { get; set; }
    public long EstimatedBytes { get; set; }
}

public sealed class BootEntryInfo
{
    public string Identifier { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class CommandResult
{
    public string FileName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
    public bool Success => ExitCode == 0;
    public string CombinedOutput => string.IsNullOrWhiteSpace(StdErr) ? StdOut : (StdOut + "\n" + StdErr).Trim();
}

public sealed class GifImportOptions
{
    public string GifPath { get; set; } = string.Empty;
    public string ThemeName { get; set; } = string.Empty;
    public int Width { get; set; } = 400;
    public int Height { get; set; } = 400;
    public int Fps { get; set; } = 15;
    public int MaxDurationMs { get; set; } = 3000;
    public string BackgroundHex { get; set; } = "000000";
    public bool SetActiveAfterImport { get; set; } = true;
}

public sealed class GifImportResult
{
    public int SourceFrameCount { get; set; }
    public int OutputFrameCount { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long EstimatedBytes { get; set; }
    public string ThemeName { get; set; } = string.Empty;
    public string ThemeDirectory { get; set; } = string.Empty;
}
