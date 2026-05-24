using HackBGRTAnimated.Core.Utilities;

namespace HackBGRTAnimated.Core.Services;

public sealed class LogService
{
    private readonly AppDataPathProvider _paths;
    private readonly EfiVolume _efiVolume;

    public LogService(AppDataPathProvider paths)
    {
        _paths = paths;
        _efiVolume = new EfiVolume(paths);
    }

    public string ReadSetupLog()
    {
        return File.Exists(_paths.SetupLogPath) ? File.ReadAllText(_paths.SetupLogPath) : "No setup log found.";
    }

    public string ReadLastActionLog()
    {
        return File.Exists(_paths.LastActionLogPath) ? File.ReadAllText(_paths.LastActionLogPath) : "No action log found.";
    }

    public string ReadGuiLog()
    {
        return File.Exists(SetupLogger.LogFilePath) ? File.ReadAllText(SetupLogger.LogFilePath) : "No GUI log found.";
    }

    public string ReadLastBootLog()
    {
        var esp = _efiVolume.LocateEspRoot();
        if (string.IsNullOrWhiteSpace(esp))
        {
            return "No boot log found.";
        }

        var path = Path.Combine(_efiVolume.GetAnimatedInstallPath(esp), "lastboot.log");
        return File.Exists(path) ? File.ReadAllText(path) : "No boot log found.";
    }
}
