using System.Windows;
using System.Windows.Controls;

namespace HackBGRTAnimated.UI.Services;

public static class InputDialog
{
    public static string? Prompt(string title, string prompt, string defaultValue = "")
    {
        var window = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = BuildContent(prompt, defaultValue, out var box),
        };

        if (Application.Current?.MainWindow is not null)
        {
            window.Owner = Application.Current.MainWindow;
        }

        var result = window.ShowDialog();
        return result == true ? box.Text : null;
    }

    private static UIElement BuildContent(string prompt, string defaultValue, out TextBox textBox)
    {
        var panel = new DockPanel { Margin = new Thickness(12) };
        var label = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(label, Dock.Top);
        panel.Children.Add(label);

        textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(textBox, Dock.Top);
        panel.Children.Add(textBox);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        ok.Click += (_, _) =>
        {
            var w = Window.GetWindow(panel);
            if (w is not null) w.DialogResult = true;
        };
        cancel.Click += (_, _) =>
        {
            var w = Window.GetWindow(panel);
            if (w is not null) w.DialogResult = false;
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);

        return panel;
    }
}
