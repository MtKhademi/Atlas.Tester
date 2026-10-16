using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using WpfButton = Wpf.Ui.Controls.Button;
using WpfSymbolIcon = Wpf.Ui.Controls.SymbolIcon;

namespace AtlasLoadTestDesktop;

internal static class AlertWindow
{
    public static void Show(DependencyObject owner, string title, string message, bool isError = false)
    {
        Show(Window.GetWindow(owner) ?? throw new InvalidOperationException("The alert owner is not attached to a window."), title, message, isError);
    }

    public static void Show(Window owner, string title, string message, bool isError = false)
    {
        var window = new Window
        {
            Owner = owner,
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.White,
            FlowDirection = FlowDirection.LeftToRight,
            ShowInTaskbar = false
        };

        var accent = isError ? Color.FromRgb(204, 76, 91) : Color.FromRgb(20, 152, 232);
        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(new System.Windows.Controls.TextBlock { Text = title, TextAlignment = TextAlignment.Left, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(16, 36, 62)), Margin = new Thickness(0, 0, 0, 12) });
        content.Children.Add(new System.Windows.Controls.TextBlock { Text = message, TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(86, 112, 135)), Margin = new Thickness(0, 0, 0, 22) });
        var button = new WpfButton { Content = new WpfSymbolIcon { Symbol = isError ? SymbolRegular.DismissCircle24 : SymbolRegular.CheckmarkCircle24 }, ToolTip = "OK", HorizontalAlignment = HorizontalAlignment.Left, Width = 42, Height = 42, Padding = new Thickness(0), Background = new SolidColorBrush(Color.FromArgb(210, accent.R, accent.G, accent.B)), Foreground = Brushes.White };
        button.Click += (_, _) => window.Close();
        content.Children.Add(button);
        window.Content = content;
        window.ShowDialog();
    }
}
