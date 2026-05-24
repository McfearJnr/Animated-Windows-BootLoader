namespace HackBGRTAnimated.Core.Models;

public sealed class UserSettings
{
    public bool FirstRunComplete { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ActiveTheme { get; set; } = "none";
    public string LastCompletedSetupStep { get; set; } = string.Empty;
    public string PanicKey { get; set; } = "esc";
    public bool PanicKeyEnabled { get; set; } = true;
    public bool AnimationPreloadDefault { get; set; } = true;
    public bool AnimationClearEachFrameDefault { get; set; } = true;
    public List<string> RecentGifPaths { get; set; } = new();
    public Dictionary<string, string> UiPreferences { get; set; } = new();
}
