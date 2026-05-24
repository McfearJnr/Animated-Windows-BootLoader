namespace HackBGRTAnimated.Core.Utilities;

[System.Obsolete("Use AppDataPathProvider instead.")]
public sealed class SetupPaths : AppDataPathProvider
{
    public SetupPaths(string? baseDirectory = null) : base(baseDirectory)
    {
    }
}
