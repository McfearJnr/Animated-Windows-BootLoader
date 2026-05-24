using System.Diagnostics;
using System.Text;
using HackBGRTAnimated.Core.Models;

namespace HackBGRTAnimated.Core.Utilities;

public static class ProcessRunner
{
    public static CommandResult Run(string fileName, string arguments, string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        try
        {
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            var result = new CommandResult
            {
                FileName = fileName,
                Arguments = arguments,
                ExitCode = process.ExitCode,
                StdOut = stdout,
                StdErr = stderr,
            };
            SetupLogger.Command(result);
            return result;
        }
        catch (Exception ex)
        {
            var result = new CommandResult
            {
                FileName = fileName,
                Arguments = arguments,
                ExitCode = -1,
                StdErr = ex.ToString(),
            };
            SetupLogger.Command(result);
            return result;
        }
    }
}
