using HackBGRTAnimated.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using System.Text;

namespace HackBGRTAnimated.WinUI;

public partial class App : Application
{
    private Window? _window;
    public AppServices Services { get; }
    public Window? MainAppWindow => _window;

    public App()
    {
        InitializeComponent();
        RequestedTheme = ApplicationTheme.Dark;
        Services = new AppServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var safeMode = string.Equals(
            Environment.GetEnvironmentVariable("HACKBGRT_WINUI_SAFE_WINDOW"),
            "1",
            StringComparison.Ordinal);

        if (safeMode)
        {
            _window = new Window
            {
                Title = "HackBGRTAnimated.WinUI (Safe Mode)",
                Content = new TextBlock
                {
                    Text = "Safe mode window started.\nRuntime initialized successfully.",
                    Margin = new Thickness(20)
                }
            };
        }
        else
        {
            try
            {
                _window = new MainWindow();
            }
            catch (Exception ex)
            {
                LogStartupException(ex);
                _window = CreateStartupErrorWindow(ex);
            }
        }

        _window.Activate();
    }

    private static Window CreateStartupErrorWindow(Exception ex)
    {
        var details = BuildStartupErrorDetails(ex);

        return new Window
        {
            Title = "HackBGRTAnimated.WinUI (Startup Error)",
            Content = new ScrollViewer
            {
                Content = new TextBox
                {
                    Text = details,
                    Margin = new Thickness(16),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true
                }
            }
        };
    }

    private static string BuildStartupErrorDetails(Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Main window failed to initialize.");
        sb.AppendLine("The app started in diagnostics mode so you can still see the error.");
        sb.AppendLine();
        sb.AppendLine($"Type: {ex.GetType().FullName}");
        sb.AppendLine($"Message: {ex.Message}");

        if (ex is COMException comEx)
        {
            sb.AppendLine($"HRESULT: 0x{comEx.HResult:X8}");
        }

        sb.AppendLine();
        sb.AppendLine(ex.StackTrace ?? "(no stack trace)");

        if (ex.InnerException is not null)
        {
            sb.AppendLine();
            sb.AppendLine("Inner exception:");
            sb.AppendLine(ex.InnerException.ToString());
        }

        return sb.ToString();
    }

    private static void LogStartupException(Exception ex)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "HackBGRTAnimated");
            Directory.CreateDirectory(dir);
            var logPath = Path.Combine(dir, "winui-startup-error.log");

            File.WriteAllText(
                logPath,
                $"[{DateTimeOffset.Now:O}] {BuildStartupErrorDetails(ex)}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // Avoid crashing while trying to log the original startup error.
        }
    }

}
