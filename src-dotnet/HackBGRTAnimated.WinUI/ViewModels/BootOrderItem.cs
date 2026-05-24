using HackBGRTAnimated.Core.Models;

namespace HackBGRTAnimated.WinUI.ViewModels;

public sealed class BootOrderItem
{
    public required string Identifier { get; init; }
    public required string Description { get; init; }
    public string Path { get; init; } = string.Empty;

    public string Display => string.IsNullOrWhiteSpace(Path)
        ? $"{Description} [{Identifier}]"
        : $"{Description} [{Identifier}]  {Path}";
}
