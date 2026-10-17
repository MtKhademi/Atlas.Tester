using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Atlas.Tester.Engine;
using Serilog;

namespace AtlasTesterDesktop;

public partial class ScenarioBuilderWindow : UserControl
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private readonly OpenApiCatalog catalog;
    private readonly List<ScenarioNodeDefinition> nodes = [];
    private int selectedNodeIndex = -1;
    private bool loadingSavedScenarios;
    private TextBox? nodeAssertionsBox;

    private ScenarioBuilderWindow(OpenApiCatalog catalog)
    {
        InitializeComponent();
        AddAssertionEditor();
        this.catalog = catalog;
        var view = CollectionViewSource.GetDefaultView(catalog.Operations);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(OpenApiOperation.TagName)));
        ApiList.ItemsSource = view;
        RefreshSavedScenarios();
        UpdateWorkflowSummary();
    }

    public static async Task<ScenarioBuilderWindow> CreateAsync(ProjectDefinition project)
    {
        var cache = new ProjectCatalogStore();
        var catalog = await cache.LoadAsync(project.Id);
        if (catalog is not null) return new ScenarioBuilderWindow(catalog);
        using var client = new HttpClient { BaseAddress = new Uri(project.BackendUrl), Timeout = TimeSpan.FromSeconds(15) };
        catalog = await OpenApiCatalog.LoadAsync(client, project.OpenApiPath);
        await cache.SaveAsync(project.Id, catalog);
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
        selectedNodeIndex = nodes.Count - 1;
        RenderWorkflow();
        UpdateWorkflowSummary();
        LoadNodeEditor();
    }

    private void RenderWorkflow()
    {
        WorkflowCanvas.Children.Clear();
        const double left = 28, cardWidth = 250, cardHeight = 62, horizontalGap = 24, verticalGap = 42;
        var availableWidth = WorkflowCanvas.ActualWidth > 0 ? WorkflowCanvas.ActualWidth : 760;
        var columns = Math.Max(1, (int)Math.Floor((availableWidth - left + horizontalGap) / (cardWidth + horizontalGap)));
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index]; var row = index / columns; var column = index % columns;
            var x = left + column * (cardWidth + horizontalGap); var top = 22 + row * (cardHeight + verticalGap);
            if (index > 0)
            {
                var p = index - 1; var px = left + (p % columns) * (cardWidth + horizontalGap); var py = 22 + (p / columns) * (cardHeight + verticalGap);
                AddConnector(new Point(px + cardWidth, py + cardHeight / 2), new Point(x, top + cardHeight / 2));
            }
            var accent = MethodColor(node.Method);
            var card = new Border { Width = cardWidth, Height = cardHeight, Padding = new Thickness(14, 8, 16, 8), Background = new SolidColorBrush(BlendWithSurface(accent)), BorderBrush = new SolidColorBrush(index == selectedNodeIndex ? Color.FromRgb(255, 255, 255) : accent), BorderThickness = new Thickness(index == selectedNodeIndex ? 2.5 : 1.8), CornerRadius = new CornerRadius(9), Cursor = Cursors.Hand, Tag = index };
            card.MouseLeftButtonUp += NodeCard_Click;
            card.Child = new StackPanel { Children = { new TextBlock { Text = $"{node.Method.ToUpperInvariant()}  {node.DisplayName}", Foreground = new SolidColorBrush(Color.FromRgb(245, 248, 252)), FontSize = 11, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = node.Path, Foreground = new SolidColorBrush(Color.FromRgb(220, 226, 234)), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) } } };
            Canvas.SetLeft(card, x); Canvas.SetTop(card, top); WorkflowCanvas.Children.Add(card);
        }
        var rows = nodes.Count == 0 ? 1 : (nodes.Count + columns - 1) / columns;
        WorkflowCanvas.Height = Math.Max(450, 44 + rows * (cardHeight + verticalGap));
    }

    private void NodeCard_Click(object sender, MouseButtonEventArgs e)
    {
        selectedNodeIndex = (int)((Border)sender).Tag;
        RenderWorkflow();
        LoadNodeEditor();
    }

    private void AddConnector(params Point[] points)
    {
        var stroke = new SolidColorBrush(Color.FromRgb(91, 126, 151));
        WorkflowCanvas.Children.Add(new Polyline { Points = new PointCollection(points), Stroke = stroke, StrokeThickness = 2 });
    }

    private void LoadNodeEditor()
    {
        var hasNode = selectedNodeIndex >= 0 && selectedNodeIndex < nodes.Count;
        NodeEditor.Visibility = hasNode ? Visibility.Visible : Visibility.Collapsed;
        NodeEditorEmptyState.Visibility = hasNode ? Visibility.Collapsed : Visibility.Visible;
        if (!hasNode) return;
        var node = nodes[selectedNodeIndex];
        NodeDisplayNameBox.Text = node.DisplayName; NodeMethodBox.Text = node.Method; NodePathBox.Text = node.Path;
        NodeBodyBox.Text = node.Request.BodyJson ?? "";
        NodeHeadersBox.Text = SerializeOrEmpty(node.Request.Headers); NodeQueryBox.Text = SerializeOrEmpty(node.Request.Query); NodePathParametersBox.Text = SerializeOrEmpty(node.Request.PathParameters);
        nodeAssertionsBox!.Text = node.Assertions is not { Count: > 0 } ? string.Empty : JsonSerializer.Serialize(node.Assertions, JsonOptions);
    }

    private void ApplyNodeEdits_Click(object sender, RoutedEventArgs e)
    {
        if (selectedNodeIndex < 0 || selectedNodeIndex >= nodes.Count) return;
        try
        {
            var previous = nodes[selectedNodeIndex];
            var method = NodeMethodBox.Text.Trim().ToUpperInvariant(); var path = NodePathBox.Text.Trim(); var title = NodeDisplayNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("Node name, method and path are required.");
            var request = new ScenarioRequestDefinition(method, path, ParseDictionary(NodeHeadersBox.Text), ParseDictionary(NodeQueryBox.Text), ParseDictionary(NodePathParametersBox.Text), EmptyToNull(NodeBodyBox.Text));
            var assertions = ParseAssertions(nodeAssertionsBox!.Text);
            nodes[selectedNodeIndex] = previous with { Method = method, Path = path, DisplayName = title, Request = request, Assertions = assertions };
            RenderWorkflow();
        }
        catch (Exception ex) { AlertWindow.Show(this, "Node data is invalid", ex.Message, true); }
    }

    private void RefreshSavedScenarios_Click(object sender, RoutedEventArgs e) => RefreshSavedScenarios();
    private void RefreshSavedScenarios()
    {
        loadingSavedScenarios = true;
        SavedScenariosBox.ItemsSource = new ScenarioStore().List();
        SavedScenariosBox.SelectedItem = null;
        loadingSavedScenarios = false;
    }

    private async void SavedScenariosBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingSavedScenarios || SavedScenariosBox.SelectedItem is not SavedScenario saved) return;
        try { await LoadScenarioAsync(await new ScenarioStore().LoadAsync(saved.Path)); }
        catch (Exception ex) { Log.Error(ex, "Saved scenario load failed: {Path}", saved.Path); AlertWindow.Show(this, "Could not load scenario", ex.Message, true); }
    }

    private void ToggleImport_Click(object sender, RoutedEventArgs e) => ImportPanel.Visibility = ImportPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private async void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(ImportJsonBox.Text, JsonOptions) ?? throw new InvalidOperationException("JSON does not contain a scenario.");
            await LoadScenarioAsync(NormalizeScenario(scenario));
            ImportPanel.Visibility = Visibility.Collapsed;
            AlertWindow.Show(this, "Scenario loaded", "The JSON scenario is now on the canvas and ready to edit.");
        }
        catch (Exception ex) { AlertWindow.Show(this, "Could not import JSON", ex.Message, true); }
    }

    private Task LoadScenarioAsync(ScenarioDefinition scenario)
    {
        NameBox.Text = scenario.Name; DescriptionBox.Text = scenario.Description;
        nodes.Clear(); nodes.AddRange(scenario.Steps);
        selectedNodeIndex = nodes.Count == 0 ? -1 : 0;
        RenderWorkflow(); UpdateWorkflowSummary(); LoadNodeEditor();
        return Task.CompletedTask;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text)) throw new InvalidOperationException("Enter a name before saving the scenario.");
            if (nodes.Count == 0) throw new InvalidOperationException("Drag at least one API into the workflow before saving.");
            var path = await new ScenarioStore().SaveAsync(new ScenarioDefinition(NameBox.Text.Trim(), DescriptionBox.Text.Trim(), nodes));
            RefreshSavedScenarios();
            AlertWindow.Show(this, "Scenario saved", $"Saved or replaced:\n{path}");
        }
        catch (Exception ex) { AlertWindow.Show(this, "Could not save scenario", ex.Message, true); }
    }

    private void OpenScenarioFolder_Click(object sender, RoutedEventArgs e)
    {
        var directory = new ScenarioStore().DirectoryPath; Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{directory}\"", UseShellExecute = true });
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        nodes.Clear(); selectedNodeIndex = -1; NameBox.Clear(); DescriptionBox.Clear(); RenderWorkflow(); UpdateWorkflowSummary(); LoadNodeEditor();
    }

    private void UpdateWorkflowSummary() => ScenarioStepCount.Text = $"{nodes.Count} step{(nodes.Count == 1 ? "" : "s")} · drag to add";
    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string SerializeOrEmpty(IReadOnlyDictionary<string, string>? values) => values is null || values.Count == 0 ? "" : JsonSerializer.Serialize(values, JsonOptions);
    private static IReadOnlyDictionary<string, string>? ParseDictionary(string json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? throw new InvalidOperationException("A request collection must be a JSON object of string values.");
    private static IReadOnlyList<ScenarioAssertionDefinition> ParseAssertions(string json) => string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<ScenarioAssertionDefinition[]>(json, JsonOptions) ?? throw new InvalidOperationException("Assertions must be a JSON array.");

    private void AddAssertionEditor()
    {
        var applyButton = NodeEditor.Children.OfType<FrameworkElement>().Single(child => Grid.GetRow(child) == 3);
        NodeEditor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(applyButton, 4);
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(new TextBlock { Text = "ASSERTIONS JSON (OPTIONAL)", Foreground = new SolidColorBrush(Color.FromRgb(174, 179, 192)), FontSize = 10, Margin = new Thickness(0, 0, 0, 4) });
        nodeAssertionsBox = new TextBox { Height = 58, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 11, ToolTip = "Example: [{\"type\":\"statusCode\",\"expectedValue\":\"200\"}]" };
        panel.Children.Add(nodeAssertionsBox);
        Grid.SetRow(panel, 3); Grid.SetColumnSpan(panel, 2); NodeEditor.Children.Add(panel);
    }
    private static ScenarioDefinition NormalizeScenario(ScenarioDefinition scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario.Name)) throw new InvalidOperationException("Scenario JSON needs a name.");
        if (scenario.Steps is null || scenario.Steps.Count == 0) throw new InvalidOperationException("Scenario JSON needs at least one node.");
        return scenario with { Steps = scenario.Steps.Select(x => x with { Id = string.IsNullOrWhiteSpace(x.Id) ? Guid.NewGuid().ToString("N") : x.Id, Request = x.Request ?? new ScenarioRequestDefinition(x.Method, x.Path) }).ToArray() };
    }
    private static Color MethodColor(string method) => method.ToUpperInvariant() switch { "GET" => Color.FromRgb(218,169,55), "POST" => Color.FromRgb(39,154,104), "PUT" => Color.FromRgb(55,132,214), "PATCH" => Color.FromRgb(155,91,196), "DELETE" => Color.FromRgb(194,75,91), _ => Color.FromRgb(91,126,151) };
    private static Color BlendWithSurface(Color accent) => Color.FromRgb((byte)((accent.R + 53) / 2), (byte)((accent.G + 55) / 2), (byte)((accent.B + 70) / 2));
}

file static class DependencyObjectExtensions
{
    public static T? FindParent<T>(this DependencyObject child) where T : DependencyObject { var parent = VisualTreeHelper.GetParent(child); while (parent is not null) { if (parent is T match) return match; parent = VisualTreeHelper.GetParent(parent); } return null; }
}
