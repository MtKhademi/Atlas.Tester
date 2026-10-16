using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Windows.Data;
using Atlas.Tester.Engine;
using Serilog;

namespace AtlasTesterDesktop;

public partial class ScenarioBuilderWindow : UserControl
{
    private readonly OpenApiCatalog catalog;
    private readonly List<ScenarioNodeDefinition> nodes = [];

    private ScenarioBuilderWindow(OpenApiCatalog catalog)
    {
        InitializeComponent(); this.catalog = catalog; var view = CollectionViewSource.GetDefaultView(catalog.Operations); view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(OpenApiOperation.TagName))); ApiList.ItemsSource = view;
    }

    public static async Task<ScenarioBuilderWindow> CreateAsync(Uri target)
    {
        Log.Information("Scenario Builder opening: target={Target}", target);
        using var client = new HttpClient { BaseAddress = target, Timeout = TimeSpan.FromSeconds(15) };
        var catalog = await OpenApiCatalog.LoadAsync(client);
        Log.Information("Scenario Builder catalog ready: operations={Count}", catalog.Operations.Count);
        return new ScenarioBuilderWindow(catalog);
    }

    private void ApiList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var item = (e.OriginalSource as DependencyObject)?.FindParent<ListBoxItem>();
        if (item?.DataContext is OpenApiOperation operation) DragDrop.DoDragDrop(ApiList, operation, DragDropEffects.Copy);
    }

    private void Canvas_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(OpenApiOperation))) return;
        var operation = (OpenApiOperation)e.Data.GetData(typeof(OpenApiOperation))!;
        nodes.Add(new ScenarioNodeDefinition(Guid.NewGuid().ToString("N"), operation.Method, operation.Path, operation.DisplayName, operation.RequiresAuthentication));
        RenderWorkflow();
        UpdateWorkflowSummary();
    }

    private void RenderWorkflow()
    {
        WorkflowCanvas.Children.Clear();
        const double left = 28;
        const double cardWidth = 250;
        const double cardHeight = 62;
        const double horizontalGap = 24;
        const double verticalGap = 42;
        var availableWidth = WorkflowCanvas.ActualWidth > 0 ? WorkflowCanvas.ActualWidth : 760;
        var columns = Math.Max(1, (int)Math.Floor((availableWidth - left + horizontalGap) / (cardWidth + horizontalGap)));

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var row = index / columns;
            var column = index % columns;
            var x = left + column * (cardWidth + horizontalGap);
            var top = 22 + row * (cardHeight + verticalGap);
            if (index > 0)
            {
                var previousRow = (index - 1) / columns;
                var previousColumn = (index - 1) % columns;
                var previousX = left + previousColumn * (cardWidth + horizontalGap);
                var previousY = 22 + previousRow * (cardHeight + verticalGap);
                if (row == previousRow)
                {
                    AddConnector(new Point(previousX + cardWidth, previousY + cardHeight / 2), new Point(x, top + cardHeight / 2));
                }
                else
                {
                    var yTurn = previousY + cardHeight + verticalGap / 2;
                    AddConnector(new Point(previousX + cardWidth, previousY + cardHeight / 2), new Point(previousX + cardWidth + horizontalGap / 2, previousY + cardHeight / 2), new Point(previousX + cardWidth + horizontalGap / 2, yTurn), new Point(x - horizontalGap / 2, yTurn), new Point(x - horizontalGap / 2, top + cardHeight / 2), new Point(x, top + cardHeight / 2));
                }
            }

            var accent = MethodColor(node.Method);
            var card = new Border
            {
                Width = cardWidth,
                Height = cardHeight,
                Padding = new Thickness(14, 10, 16, 10),
                Background = new SolidColorBrush(BlendWithSurface(accent)),
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1.8),
                CornerRadius = new CornerRadius(9)
            };
            card.Child = new TextBlock
            {
                Text = node.Path,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 248, 252)),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(card, x); Canvas.SetTop(card, top); WorkflowCanvas.Children.Add(card);
        }

        var rows = nodes.Count == 0 ? 1 : (nodes.Count + columns - 1) / columns;
        WorkflowCanvas.Height = Math.Max(450, 44 + rows * (cardHeight + verticalGap));
    }

    private void AddConnector(params Point[] points)
    {
        var stroke = new SolidColorBrush(Color.FromRgb(91, 126, 151));
        WorkflowCanvas.Children.Add(new Polyline { Points = new PointCollection(points), Stroke = stroke, StrokeThickness = 2, SnapsToDevicePixels = true });
        var end = points[^1];
        var previous = points[^2];
        var pointsForArrow = previous.X <= end.X
            ? new PointCollection([new Point(end.X - 7, end.Y - 4), new Point(end.X - 7, end.Y + 4), new Point(end.X, end.Y)])
            : new PointCollection([new Point(end.X + 7, end.Y - 4), new Point(end.X + 7, end.Y + 4), new Point(end.X, end.Y)]);
        WorkflowCanvas.Children.Add(new Polygon { Points = pointsForArrow, Fill = stroke });
    }

    private static Color MethodColor(string method) => method.ToUpperInvariant() switch
    {
        "GET" => Color.FromRgb(218, 169, 55),
        "POST" => Color.FromRgb(39, 154, 104),
        "PUT" => Color.FromRgb(55, 132, 214),
        "PATCH" => Color.FromRgb(155, 91, 196),
        "DELETE" => Color.FromRgb(194, 75, 91),
        _ => Color.FromRgb(91, 126, 151)
    };

    private static Color BlendWithSurface(Color accent) => Color.FromRgb(
        (byte)((accent.R + 53) / 2),
        (byte)((accent.G + 55) / 2),
        (byte)((accent.B + 70) / 2));

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        Log.Information("Scenario save requested: name={Name}, nodes={NodeCount}", NameBox.Text, nodes.Count);
        if (string.IsNullOrWhiteSpace(NameBox.Text)) { Log.Warning("Scenario save skipped because scenario name is empty"); AlertWindow.Show(this, "Scenario name required", "Enter a name before saving the scenario.", true); NameBox.Focus(); return; }
        if (nodes.Count == 0) { Log.Warning("Scenario save skipped because no nodes were added"); AlertWindow.Show(this, "Empty scenario", "Drag at least one API into the workflow before saving.", true); return; }
        var store = new ScenarioStore(System.IO.Path.Combine(AppContext.BaseDirectory, "scenarios")); var path = await store.SaveAsync(new ScenarioDefinition(NameBox.Text, DescriptionBox.Text, nodes)); Log.Information("Scenario saved: path={Path}, bytes={Bytes}", path, new FileInfo(path).Length); AlertWindow.Show(this, "Scenario saved", $"The scenario was saved successfully:\n{path}");
    }

    private void Clear_Click(object sender, RoutedEventArgs e) { nodes.Clear(); WorkflowCanvas.Children.Clear(); WorkflowCanvas.Height = 450; UpdateWorkflowSummary(); }

    private void UpdateWorkflowSummary()
    {
        ScenarioStepCount.Text = $"{nodes.Count} step{(nodes.Count == 1 ? "" : "s")} · drag to add";
        WorkflowEmptyState.Visibility = nodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

file static class DependencyObjectExtensions
{
    public static T? FindParent<T>(this DependencyObject child) where T : DependencyObject { var parent = VisualTreeHelper.GetParent(child); while (parent is not null) { if (parent is T match) return match; parent = VisualTreeHelper.GetParent(parent); } return null; }
}
