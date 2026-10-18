using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Atlas.Tester.Engine;

namespace AtlasTesterDesktop;

public sealed class NodeDetailsWindow : Window
{
    private readonly ScenarioNodeDefinition original;
    private readonly TextBox title = new(), path = new(), body = new(), captures = new(), assertions = new();
    private readonly Border methodBadge = new();
    public ScenarioNodeDefinition? UpdatedNode { get; private set; }

    public NodeDetailsWindow(ScenarioNodeDefinition node, IEnumerable<string> earlierVariables)
    {
        original = node;
        Title = "Node details"; Width = 760; Height = 720; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResize; Background = new SolidColorBrush(Color.FromRgb(30, 32, 42));
        Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock { Text = "NODE DETAILS", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(85,181,255)) });
        root.Children.Add(new TextBlock { Text = "Configure this API step. Use {{variable}} values captured by earlier nodes.", Foreground = new SolidColorBrush(Color.FromRgb(174,179,192)), Margin = new Thickness(0,5,0,18) });
        var identity = new Grid { Margin = new Thickness(0,0,0,12) };
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) }); identity.ColumnDefinitions.Add(new ColumnDefinition()); identity.ColumnDefinitions.Add(new ColumnDefinition());
        StyleMethod(node.Method);
        AddTopField(identity, "TYPE", methodBadge, 0); AddTopField(identity, "ADDRESS", path, 1, node.Path); AddTopField(identity, "NAME", title, 2, node.DisplayName); root.Children.Add(identity);
        Add(root, "REQUEST BODY", body, node.Request.BodyJson ?? "", 110);
        Add(root, "CAPTURES JSON", captures, node.Captures.Count == 0 ? "" : JsonSerializer.Serialize(node.Captures, new JsonSerializerOptions { WriteIndented = true }), 82);
        Add(root, "ASSERTIONS JSON", assertions, node.Assertions.Count == 0 ? "" : JsonSerializer.Serialize(node.Assertions, new JsonSerializerOptions { WriteIndented = true }), 82);
        var variables = earlierVariables.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        root.Children.Add(new TextBlock { Text = variables.Length == 0 ? "Earlier variables: none" : $"Earlier variables: {string.Join(", ", variables.Select(x => "{{" + x + "}}"))}", Foreground = new SolidColorBrush(Color.FromRgb(143,150,168)), FontSize = 11, Margin = new Thickness(0,0,0,14) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,10,0,0) };
        var cancel = new Button { Content = "Cancel", Margin = new Thickness(0,0,8,0), Padding = new Thickness(16,8,16,8), Background = new SolidColorBrush(Color.FromRgb(69,72,88)), Foreground = Brushes.White, BorderThickness = new Thickness(0) }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Apply node changes", Padding = new Thickness(16,8,16,8), Background = new SolidColorBrush(Color.FromRgb(33,111,164)), Foreground = Brushes.White, BorderThickness = new Thickness(0) }; save.Click += Save_Click; actions.Children.Add(cancel); actions.Children.Add(save); root.Children.Add(actions);
        var surface = new Border { Background = new SolidColorBrush(Color.FromRgb(53,55,70)), BorderBrush = new SolidColorBrush(Color.FromRgb(69,72,88)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Child = root, Margin = new Thickness(10) };
        Content = new ScrollViewer { Content = surface, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromRgb(30,32,42)) };
    }

    private static void Add(Panel panel, string label, TextBox input, string value, double height = 30)
    {
        panel.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(174,179,192)), FontSize = 10, Margin = new Thickness(0,0,0,4) });
        input.Text = value; input.Height = height; input.AcceptsReturn = height > 40; input.TextWrapping = TextWrapping.Wrap; input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; input.Margin = new Thickness(0,0,0,12); input.Padding = new Thickness(9,6,9,6); input.Background = new SolidColorBrush(Color.FromRgb(43,51,64)); input.Foreground = new SolidColorBrush(Color.FromRgb(242,244,247)); input.BorderBrush = new SolidColorBrush(Color.FromRgb(93,114,134)); input.BorderThickness = new Thickness(1); panel.Children.Add(input);
    }

    private static void AddTopField(Grid grid, string label, FrameworkElement input, int column, string? value = null)
    {
        var panel = new StackPanel { Margin = new Thickness(column == 0 ? 0 : 5, 0, column == 2 ? 0 : 5, 0) };
        panel.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(174,179,192)), FontSize = 10, Margin = new Thickness(0,0,0,4) });
        if (input is TextBox text) text.Text = value ?? string.Empty;
        input.Height = 32;
        if (input is Control control) { control.Background = new SolidColorBrush(Color.FromRgb(43,51,64)); control.Foreground = new SolidColorBrush(Color.FromRgb(242,244,247)); control.BorderBrush = new SolidColorBrush(Color.FromRgb(93,114,134)); control.BorderThickness = new Thickness(1); }
        if (input is TextBox textBox) textBox.Padding = new Thickness(9,4,9,4);
        if (input is ComboBox comboBox) comboBox.Padding = new Thickness(9,4,9,4);
        panel.Children.Add(input); Grid.SetColumn(panel, column); grid.Children.Add(panel);
    }

    private void StyleMethod(string value)
    {
        var color = value switch { "POST" => Color.FromRgb(39,154,104), "GET" => Color.FromRgb(218,169,55), "PUT" => Color.FromRgb(55,132,214), "PATCH" => Color.FromRgb(155,91,196), "DELETE" => Color.FromRgb(194,75,91), _ => Color.FromRgb(91,126,151) };
        methodBadge.Background = new SolidColorBrush(color); methodBadge.CornerRadius = new CornerRadius(5); methodBadge.Padding = new Thickness(9,4,9,4); methodBadge.Child = new TextBlock { Text = value.ToUpperInvariant(), Foreground = Brushes.White, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selectedMethod = original.Method;
            if (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(selectedMethod) || string.IsNullOrWhiteSpace(path.Text)) throw new InvalidOperationException("Name, method and path are required.");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var captureValues = string.IsNullOrWhiteSpace(captures.Text) ? [] : JsonSerializer.Deserialize<ScenarioCaptureDefinition[]>(captures.Text, options) ?? [];
            var assertionValues = string.IsNullOrWhiteSpace(assertions.Text) ? [] : JsonSerializer.Deserialize<ScenarioAssertionDefinition[]>(assertions.Text, options) ?? [];
            UpdatedNode = original with { DisplayName = title.Text.Trim(), Method = selectedMethod, Path = path.Text.Trim(), Request = original.Request with { Method = selectedMethod, Path = path.Text.Trim(), BodyJson = string.IsNullOrWhiteSpace(body.Text) ? null : body.Text.Trim() }, Captures = captureValues, Assertions = assertionValues };
            DialogResult = true;
        }
        catch (Exception ex) { AlertWindow.Show(this, "Node data is invalid", ex.Message, true); }
    }
}
