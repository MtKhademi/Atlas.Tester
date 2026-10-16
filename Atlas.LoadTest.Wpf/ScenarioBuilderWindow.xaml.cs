using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Data;
using Atlas.LoadTest.Engine;
using Serilog;

namespace AtlasLoadTestDesktop;

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
        var node = new ScenarioNodeDefinition(Guid.NewGuid().ToString("N"), operation.Method, operation.Path, operation.DisplayName, operation.RequiresAuthentication); nodes.Add(node); RenderNode(node); UpdateWorkflowSummary();
    }

    private void RenderNode(ScenarioNodeDefinition node)
    {
        var card = new Border { Width = 300, Padding = new Thickness(16), Margin = new Thickness(18), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(20, 152, 232)), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(14) };
        var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = node.Method, Foreground = new SolidColorBrush(Color.FromRgb(20, 152, 232)), FontWeight = FontWeights.Bold }); stack.Children.Add(new TextBlock { Text = node.DisplayName, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(16, 36, 62)), Margin = new Thickness(0, 6, 0, 4) }); stack.Children.Add(new TextBlock { Text = node.Path, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(86, 112, 135)) });
        card.Child = stack; Canvas.SetLeft(card, 20); Canvas.SetTop(card, (nodes.Count - 1) * 125 + 20); WorkflowCanvas.Children.Add(card);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        Log.Information("Scenario save requested: name={Name}, nodes={NodeCount}", NameBox.Text, nodes.Count);
        if (string.IsNullOrWhiteSpace(NameBox.Text)) { Log.Warning("Scenario save skipped because scenario name is empty"); AlertWindow.Show(this, "Scenario name required", "Enter a name before saving the scenario.", true); NameBox.Focus(); return; }
        if (nodes.Count == 0) { Log.Warning("Scenario save skipped because no nodes were added"); AlertWindow.Show(this, "Empty scenario", "Drag at least one API into the workflow before saving.", true); return; }
        var store = new ScenarioStore(Path.Combine(AppContext.BaseDirectory, "scenarios")); var path = await store.SaveAsync(new ScenarioDefinition(NameBox.Text, DescriptionBox.Text, nodes)); Log.Information("Scenario saved: path={Path}, bytes={Bytes}", path, new FileInfo(path).Length); AlertWindow.Show(this, "Scenario saved", $"The scenario was saved successfully:\n{path}");
    }

    private void Clear_Click(object sender, RoutedEventArgs e) { nodes.Clear(); WorkflowCanvas.Children.Clear(); UpdateWorkflowSummary(); }

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
