using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AtlasTesterDesktop;

internal sealed class LoadingWindow : Window
{
    public LoadingWindow(Window owner, string title, string message)
    {
        Owner = owner; Title = title; Width = 380; Height = 175;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Background = new SolidColorBrush(Color.FromRgb(41, 42, 56));
        Content = new StackPanel
        {
            Margin = new Thickness(28),
            Children =
            {
                new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 19, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = message, Foreground = new SolidColorBrush(Color.FromRgb(174, 179, 192)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 16) },
                new ProgressBar { IsIndeterminate = true, Height = 6 }
            }
        };
    }
}
