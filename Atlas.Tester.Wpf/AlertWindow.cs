using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AtlasTesterDesktop;

internal static class AlertWindow
{
    public static void Show(DependencyObject owner, string title, string message, bool isError = false)
    {
        Show(Window.GetWindow(owner) ?? throw new InvalidOperationException("The alert owner is not attached to a window."), title, message, isError);
    }

    public static void Show(Window owner, string title, string message, bool isError = false)
    {
        var window = CreateWindow(owner, title, 440);
        var accent = isError ? Color.FromRgb(220, 105, 115) : Color.FromRgb(85, 181, 255);
        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(new TextBlock { Text = isError ? "ERROR" : "ATLAS TESTER", Foreground = Brush(accent), FontSize = 11, FontWeight = FontWeights.Bold });
        content.Children.Add(new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 7, 0, 10) });
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = Brush(Color.FromRgb(210, 216, 228)), Margin = new Thickness(0, 0, 0, 22) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var close = CreateButton("OK", Brush(accent));
        close.Click += (_, _) => window.Close();
        actions.Children.Add(close);
        content.Children.Add(actions);
        window.Content = content;
        window.ShowDialog();
    }

    public static bool Confirm(Window owner, string title, string message, bool isDangerous = false)
    {
        var confirmed = false;
        var window = CreateWindow(owner, title, 440);
        var accent = isDangerous ? Color.FromRgb(220, 105, 115) : Color.FromRgb(85, 181, 255);
        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(new TextBlock { Text = isDangerous ? "CONFIRM DELETION" : "CONFIRMATION", Foreground = Brush(accent), FontSize = 11, FontWeight = FontWeights.Bold });
        content.Children.Add(new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 7, 0, 10) });
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = Brush(Color.FromRgb(210, 216, 228)), Margin = new Thickness(0, 0, 0, 22) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = CreateButton("Cancel", Brushes.Transparent, Brush(Color.FromRgb(210, 216, 228)), Brush(Color.FromRgb(91, 98, 116)));
        cancel.Click += (_, _) => window.Close();
        var confirm = CreateButton("Delete", Brush(accent));
        confirm.Click += (_, _) => { confirmed = true; window.Close(); };
        actions.Children.Add(cancel); actions.Children.Add(confirm);
        content.Children.Add(actions);
        window.Content = content;
        window.ShowDialog();
        return confirmed;
    }

    private static Window CreateWindow(Window owner, string title, double width) => new()
    {
        Owner = owner,
        Title = title,
        Width = width,
        SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ResizeMode = ResizeMode.NoResize,
        Background = Brush(Color.FromRgb(41, 42, 56)),
        FlowDirection = FlowDirection.LeftToRight,
        ShowInTaskbar = false
    };

    private static Button CreateButton(string text, Brush background, Brush? foreground = null, Brush? border = null) => new()
    {
        Content = text,
        Padding = new Thickness(16, 8, 16, 8),
        Margin = new Thickness(8, 0, 0, 0),
        Background = background,
        Foreground = foreground ?? Brushes.White,
        BorderBrush = border ?? background,
        BorderThickness = border is null ? new Thickness(0) : new Thickness(1)
    };

    private static SolidColorBrush Brush(Color color) => new(color);
}
