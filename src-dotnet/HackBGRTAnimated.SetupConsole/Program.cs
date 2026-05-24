using HackBGRTAnimated.Core.Services;
using HackBGRTAnimated.Core.Utilities;

var paths = new AppDataPathProvider(AppContext.BaseDirectory);
var settings = new SettingsService(paths);
var themes = new ThemeManager(paths, settings);
var statusService = new SetupStatusService(paths, themes);
var ops = new SetupOperations(paths, settings, themes);
var migration = new RepoDataMigrationService(paths, settings);

var legacyItems = migration.DetectLegacyItems();
if (legacyItems.Count > 0)
{
    Console.WriteLine("Detected repo-local generated/user data:");
    foreach (var item in legacyItems)
    {
        Console.WriteLine($"  - {item}");
    }
    Console.Write("Migrate this data to AppData now? (y/N): ");
    var migrate = Console.ReadKey();
    Console.WriteLine();
    if (migrate.KeyChar is 'y' or 'Y')
    {
        Console.Write("Archive old repo data after migration? (y/N): ");
        var archive = Console.ReadKey();
        Console.WriteLine();
        var result = migration.Migrate(archive.KeyChar is 'y' or 'Y');
        Console.WriteLine(result);
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }
}

while (true)
{
    Console.Clear();
    var status = statusService.GetStatus();
    Console.WriteLine("HackBGRT-Animated Setup Console (Core-backed)");
    Console.WriteLine("===========================================");
    Console.WriteLine($"AppData root: {status.AppDataRoot}");
    Console.WriteLine($"Installed: {status.Installed}");
    Console.WriteLine($"EFI folder found: {status.EfiFolderFound}");
    Console.WriteLine($"Boot entry found: {status.BootEntryFound}");
    Console.WriteLine($"In boot order: {status.InBootOrder}");
    Console.WriteLine($"Default: {status.IsDefault}");
    Console.WriteLine($"Active theme: {status.ActiveTheme}");
    Console.WriteLine();
    Console.WriteLine("1. Install / Update");
    Console.WriteLine("2. Sync Config to EFI");
    Console.WriteLine("3. Restore Windows Boot Manager First");
    Console.WriteLine("4. Restart PC");
    Console.WriteLine("5. Open AppData Folder");
    Console.WriteLine("6. Launch legacy setup.exe");
    Console.WriteLine("Q. Quit");

    var key = Console.ReadKey(true).Key;
    try
    {
        switch (key)
        {
            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                ops.InstallOrUpdate();
                break;
            case ConsoleKey.D2:
            case ConsoleKey.NumPad2:
                ops.SyncAnimatedConfig();
                break;
            case ConsoleKey.D3:
            case ConsoleKey.NumPad3:
                ops.RestoreWindowsFirst();
                break;
            case ConsoleKey.D4:
            case ConsoleKey.NumPad4:
                ops.RestartPc();
                return;
            case ConsoleKey.D5:
            case ConsoleKey.NumPad5:
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{paths.AppDataRoot}\"") { UseShellExecute = true });
                break;
            case ConsoleKey.D6:
            case ConsoleKey.NumPad6:
                if (File.Exists(paths.SetupExePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(paths.SetupExePath) { UseShellExecute = true, WorkingDirectory = paths.RepoRoot });
                }
                else
                {
                    Console.WriteLine("setup.exe not found.");
                    Console.ReadKey(true);
                }
                break;
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                return;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }
}
