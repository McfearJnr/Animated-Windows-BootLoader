using System.Collections.ObjectModel;
using HackBGRTAnimated.WinUI.Services;
using HackBGRTAnimated.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackBGRTAnimated.WinUI.Views;

public sealed partial class BootOrderPage : Page
{
    private readonly App _app;
    private readonly ObservableCollection<BootOrderItem> _items = [];

    public BootOrderPage()
    {
        InitializeComponent();
        _app = (App)Application.Current;
        BootOrderList.ItemsSource = _items;
        if (!UiMotionSettings.AreAnimationsEnabled)
        {
            BootOrderList.ItemContainerTransitions = null;
        }
        RefreshList();
    }

    private BootOrderItem? SelectedItem => BootOrderList.SelectedItem as BootOrderItem;

    private void RefreshList()
    {
        _items.Clear();

        var (entries, order, _) = _app.Services.BootEntries.ReadFirmwareEntries();
        foreach (var id in order)
        {
            var entry = entries.FirstOrDefault(e => string.Equals(e.Identifier, id, StringComparison.OrdinalIgnoreCase));
            _items.Add(new BootOrderItem
            {
                Identifier = id,
                Description = entry?.Description ?? "(unknown)",
                Path = entry?.Path ?? string.Empty,
            });
        }

        BootEmptyState.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BootOrderList.Visibility = _items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshList();

    private void MoveUp_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem;
        if (selected is null)
        {
            return;
        }

        var idx = _items.IndexOf(selected);
        if (idx <= 0)
        {
            return;
        }

        _items.Move(idx, idx - 1);
        BootOrderList.SelectedItem = selected;
    }

    private void MoveDown_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem;
        if (selected is null)
        {
            return;
        }

        var idx = _items.IndexOf(selected);
        if (idx < 0 || idx >= _items.Count - 1)
        {
            return;
        }

        _items.Move(idx, idx + 1);
        BootOrderList.SelectedItem = selected;
    }

    private async void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmAsync("Save Boot Order", "This changes firmware boot order only. Save now?"))
        {
            return;
        }

        try
        {
            _app.Services.BootEntries.SaveDisplayOrder(_items.Select(i => i.Identifier).ToList());
            RefreshList();
            await ShowMessageAsync("Boot order updated.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void WindowsFirst_OnClick(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmAsync("Restore Windows Boot Manager First", "Move Windows Boot Manager to first position?"))
        {
            return;
        }

        try
        {
            _app.Services.BootEntries.RestoreWindowsFirst();
            RefreshList();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void AnimatedFirst_OnClick(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmAsync("Put HackBGRT-Animated First", "This makes HackBGRT-Animated first in firmware boot order. Continue?"))
        {
            return;
        }

        try
        {
            _app.Services.BootEntries.PutAnimatedFirst();
            RefreshList();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = "Confirm",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "HackBGRT-Animated",
            Content = message,
            CloseButtonText = "OK",
        };
        await dialog.ShowAsync();
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
        };
        await dialog.ShowAsync();
    }
}
