using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Atlas.Tester.Engine;

namespace AtlasTesterDesktop;

internal sealed class ProjectDialog : Window
{
    private readonly TextBox name = new(); private readonly TextBox url = new(); private readonly TextBox openApi = new();
    public ProjectDefinition Project { get; private set; } = null!;
    public ProjectDialog(Window owner)
    {
        Owner = owner; Title = "Add project"; Width = 520; Height = 390; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = ColorToBrush(Color.FromRgb(41, 42, 56));
        var grid = new Grid { Margin = new Thickness(28) }; for (var i = 0; i < 5; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = "NEW PROJECT", Foreground = ColorToBrush(Color.FromRgb(85, 181, 255)), FontSize = 12, FontWeight = FontWeights.Bold });
        AddField(grid, "Project name", name, 1, "Atlas API"); AddField(grid, "Backend URL", url, 2, "http://localhost:5000"); AddField(grid, "OpenAPI path", openApi, 3, "/openapi/v1.json");
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0), Background = Brushes.Transparent, Foreground = ColorToBrush(Color.FromRgb(210, 216, 228)), BorderBrush = ColorToBrush(Color.FromRgb(91, 98, 116)) }; cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = "Save project", Padding = new Thickness(14, 8, 14, 8), Background = ColorToBrush(Color.FromRgb(33, 111, 164)), Foreground = Brushes.White, BorderThickness = new Thickness(0) }; save.Click += (_, _) => { if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(url.Text)) return; Project = new ProjectDefinition(Guid.NewGuid().ToString("N"), name.Text.Trim(), "", url.Text.Trim().TrimEnd('/'), string.IsNullOrWhiteSpace(openApi.Text) ? "/openapi/v1.json" : openApi.Text.Trim(), DateTimeOffset.UtcNow); DialogResult = true; }; actions.Children.Add(cancel); actions.Children.Add(save); Grid.SetRow(actions, 4); grid.Children.Add(actions); Content = grid;
    }
    private static void AddField(Grid grid, string label, TextBox box, int row, string value) { box.Text = value; box.Foreground = Brushes.White; box.Background = ColorToBrush(Color.FromRgb(53, 55, 70)); box.BorderBrush = ColorToBrush(Color.FromRgb(91, 98, 116)); box.Padding = new Thickness(10, 8, 10, 8); var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) }; panel.Children.Add(new TextBlock { Text = label.ToUpperInvariant(), Foreground = ColorToBrush(Color.FromRgb(174, 179, 192)), FontSize = 10, FontWeight = FontWeights.SemiBold }); panel.Children.Add(box); Grid.SetRow(panel, row); grid.Children.Add(panel); }
    private static SolidColorBrush ColorToBrush(Color color) => new(color);
}
