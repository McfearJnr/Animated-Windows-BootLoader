using System.Windows;
using System.Windows.Controls;
using HackBGRTAnimated.UI.Services;
using HackBGRTAnimated.UI.Views;

namespace HackBGRTAnimated.UI;

public partial class MainWindow : Window
{
    private readonly AppServices _services = new();
    private bool _migrationPromptShown;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        NavList.SelectedIndex = 0;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_migrationPromptShown)
        {
            return;
        }

        _migrationPromptShown = true;
        var legacyItems = _services.Migration.DetectLegacyItems();
        if (legacyItems.Count == 0)
        {
            return;
        }

        var preview = string.Join(Environment.NewLine, legacyItems.Take(8));
        if (legacyItems.Count > 8)
        {
            preview += Environment.NewLine + "...";
        }

        var migrate = MessageBox.Show(
            "Found local/generated data inside the repository:\n\n" + preview +
            "\n\nMigrate this data into AppData now?",
            "HackBGRT-Animated Migration",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (migrate != MessageBoxResult.Yes)
        {
            return;
        }

        var archive = MessageBox.Show(
            "Archive old repo-local data under .migrated-local-data after migration?",
            "HackBGRT-Animated Migration",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

        try
        {
            var summary = _services.Migration.Migrate(archive);
            MessageBox.Show(summary, "Migration complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Migration failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NavList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem item)
        {
            return;
        }

        var key = item.Tag?.ToString();
        PageHost.Content = key switch
        {
            "dashboard" => new DashboardPage(_services),
            "themes" => new ThemesPage(_services),
            "import" => new ImportGifPage(_services),
            "boot" => new BootOrderPage(_services),
            "config" => new ConfigPage(_services),
            "logs" => new LogsPage(_services),
            "advanced" => new AdvancedPage(_services),
            _ => new DashboardPage(_services),
        };
    }
}
