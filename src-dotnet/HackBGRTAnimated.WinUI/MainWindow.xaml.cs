using System;
using System.Collections.Generic;
using System.Linq;
using HackBGRTAnimated.WinUI.Services;
using HackBGRTAnimated.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace HackBGRTAnimated.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Type> _pageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = typeof(DashboardPage),
        ["themes"] = typeof(ThemesPage),
        ["import"] = typeof(ImportGifPage),
        ["boot"] = typeof(BootOrderPage),
        ["config"] = typeof(ConfigPage),
        ["logs"] = typeof(LogsPage),
        ["advanced"] = typeof(AdvancedPage),
        ["oobe"] = typeof(OobePage),
    };

    private bool _inOobe;
    private readonly bool _animationsEnabled = UiMotionSettings.AreAnimationsEnabled;

    public MainWindow()
    {
        InitializeComponent();

        var noNavMode = string.Equals(
            Environment.GetEnvironmentVariable("HACKBGRT_WINUI_NO_NAV"),
            "1",
            StringComparison.Ordinal);

        if (noNavMode)
        {
            ContentFrame.Content = new TextBlock
            {
                Text = "MainWindow loaded (no navigation mode).",
                Margin = new Thickness(20)
            };
            RootNavigation.IsPaneOpen = false;
            RootNavigation.IsPaneVisible = false;
            return;
        }

        var app = (App)Application.Current;
        var settings = app.Services.Settings.Load();

        if (!settings.FirstRunComplete)
        {
            EnterOobe();
            return;
        }

        SelectAndNavigate("dashboard");
    }

    public void ExitOobeToDashboard()
    {
        _inOobe = false;
        RootNavigation.IsPaneVisible = true;
        RootNavigation.IsEnabled = true;
        SelectAndNavigate("dashboard");
    }

    private void EnterOobe()
    {
        _inOobe = true;
        RootNavigation.IsPaneVisible = false;
        RootNavigation.IsEnabled = false;
        Navigate("oobe");
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_inOobe)
        {
            return;
        }

        if (args.IsSettingsSelected)
        {
            SelectAndNavigate("config");
            return;
        }

        if (args.SelectedItemContainer?.Tag is not string key)
        {
            return;
        }

        if (string.Equals(key, "oobe", StringComparison.OrdinalIgnoreCase))
        {
            EnterOobe();
            return;
        }

        Navigate(key);
    }

    private void SelectAndNavigate(string key)
    {
        var item = EnumerateNavigationItems().FirstOrDefault(i => string.Equals(i.Tag?.ToString(), key, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            RootNavigation.SelectedItem = item;
        }

        Navigate(key);
    }

    private IEnumerable<NavigationViewItem> EnumerateNavigationItems()
    {
        foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
        {
            yield return item;
        }

        foreach (var item in RootNavigation.FooterMenuItems.OfType<NavigationViewItem>())
        {
            yield return item;
        }
    }

    private void Navigate(string key)
    {
        if (!_pageMap.TryGetValue(key, out var pageType))
        {
            pageType = typeof(DashboardPage);
        }

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            NavigationTransitionInfo transition = _animationsEnabled
                ? new DrillInNavigationTransitionInfo()
                : new SuppressNavigationTransitionInfo();
            ContentFrame.Navigate(pageType, null, transition);
        }
    }
}
