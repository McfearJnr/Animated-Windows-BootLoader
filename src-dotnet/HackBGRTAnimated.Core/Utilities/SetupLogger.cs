using System.Diagnostics;
using HackBGRTAnimated.Core.Models;

namespace HackBGRTAnimated.Core.Utilities;

public static class SetupLogger
{
    private static readonly object Sync = new();
    public static string LogFilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HackBGRT-Animated",
        "logs",
        "setup.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Command(CommandResult result)
    {
        Write("CMD", $"{result.FileName} {result.Arguments} => {result.ExitCode}\nSTDOUT:\n{result.StdOut}\nSTDERR:\n{result.StdErr}");
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        lock (Sync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath) ?? Environment.CurrentDirectory);
            File.AppendAllText(LogFilePath, line + Environment.NewLine);
        }
        Debug.WriteLine(line);
    }
}
