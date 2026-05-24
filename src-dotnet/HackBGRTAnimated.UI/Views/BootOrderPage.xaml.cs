using System.Windows;
using System.Windows.Controls;
using HackBGRTAnimated.Core.Models;
using HackBGRTAnimated.UI.Services;

namespace HackBGRTAnimated.UI.Views;

public partial class BootOrderPage : UserControl
{
    private readonly AppServices _services;
    private List<BootEntryInfo> _ordered = new();

    public BootOrderPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        RefreshList();
    }

    private void RefreshList()
    {
        try
        {
            var (entries, order, _) = _services.BootEntries.ReadFirmwareEntries();
            _ordered = order.Select(id => entries.FirstOrDefault(e => string.Equals(e.Identifier, id, StringComparison.OrdinalIgnoreCase))
                                     ?? new BootEntryInfo { Identifier = id, Description = "(unknown)", Path = string.Empty }).ToList();
            BootList.ItemsSource = null;
            BootList.ItemsSource = _ordered.Select((e, i) => $"{i + 1}. {e.Description} [{e.Identifier}] {e.Path}").ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshList();

    private void MoveUp_OnClick(object sender, RoutedEventArgs e)
    {
        var idx = BootList.SelectedIndex;
        if (idx <= 0) return;
        (_ordered[idx - 1], _ordered[idx]) = (_ordered[idx], _ordered[idx - 1]);
        RefreshFromMemory(idx - 1);
    }

    private void MoveDown_OnClick(object sender, RoutedEventArgs e)
    {
        var idx = BootList.SelectedIndex;
        if (idx < 0 || idx >= _ordered.Count - 1) return;
        (_ordered[idx + 1], _ordered[idx]) = (_ordered[idx], _ordered[idx + 1]);
        RefreshFromMemory(idx + 1);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("This changes firmware boot order only. Save now?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            _services.BootEntries.SaveDisplayOrder(_ordered.Select(o => o.Identifier).ToList());
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void WindowsFirst_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _services.BootEntries.RestoreWindowsFirst();
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AnimatedFirst_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _services.BootEntries.PutAnimatedFirst();
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshFromMemory(int selected)
    {
        BootList.ItemsSource = null;
        BootList.ItemsSource = _ordered.Select((e, i) => $"{i + 1}. {e.Description} [{e.Identifier}] {e.Path}").ToList();
        BootList.SelectedIndex = selected;
    }
}
