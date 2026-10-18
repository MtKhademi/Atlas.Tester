using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Atlas.Tester.Engine;

namespace AtlasTesterDesktop;

public sealed class ScenarioVariablesWindow : Window
{
    private readonly List<ScenarioVariableDefinition> items;
    private readonly ListBox list = new();
    private readonly TextBox name = new(), value = new();
    private readonly CheckBox secret = new() { Content = "Secret (mask in reports)" };
    public IReadOnlyList<ScenarioVariableDefinition> Variables => items;

    public ScenarioVariablesWindow(IEnumerable<ScenarioVariableDefinition> variables)
    {
        items = variables.ToList(); Title = "Scenario variables"; Width = 580; Height = 620; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(30,32,42));
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new StackPanel();
        header.Children.Add(new TextBlock { Text = "GLOBAL VARIABLES", Foreground = new SolidColorBrush(Color.FromRgb(85,181,255)), FontSize = 18, FontWeight = FontWeights.Bold });
        header.Children.Add(new TextBlock { Text = "Use values in nodes with {{variableName}}. Apply changes here, then use Save scenario in the Builder to persist them.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(174,179,192)), Margin = new Thickness(0,4,0,14) });
        Grid.SetRow(header, 0); root.Children.Add(header);
        list.Background = new SolidColorBrush(Color.FromRgb(43,51,64)); list.Foreground = Brushes.White; list.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto); list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled); list.Margin = new Thickness(0,0,0,12); Grid.SetRow(list, 1); root.Children.Add(list); Refresh();
        var editor = new StackPanel();
        editor.Children.Add(new TextBlock { Text = "NAME", Foreground = new SolidColorBrush(Color.FromRgb(174,179,192)), FontSize = 10, Margin = new Thickness(0,0,0,3) }); editor.Children.Add(name);
        editor.Children.Add(new TextBlock { Text = "VALUE", Foreground = new SolidColorBrush(Color.FromRgb(174,179,192)), FontSize = 10, Margin = new Thickness(0,8,0,3) }); editor.Children.Add(value); editor.Children.Add(secret);
        Grid.SetRow(editor, 2); root.Children.Add(editor);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,12,0,0) };
        var add = new Button { Content = "Add variable", Margin = new Thickness(0,0,18,0) }; add.Click += Add; var done = new Button { Content = "Apply variables", ToolTip = "Return these changes to the Builder. Save scenario there to write the JSON file." }; done.Click += (_, _) => DialogResult = true; actions.Children.Add(add); actions.Children.Add(done); Grid.SetRow(actions, 3); root.Children.Add(actions);
        Content = root;
    }
    private void Refresh()
    {
        list.Items.Clear();
        foreach (var variable in items.ToArray())
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(8, 3, 8, 3) };
            var remove = new Button { Content = "Remove", Tag = variable, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(10, 0, 0, 0) };
            remove.Click += Remove;
            DockPanel.SetDock(remove, Dock.Right); row.Children.Add(remove);
            row.Children.Add(new TextBlock { Text = $"{variable.Name} = {(variable.IsSecret ? "••••••" : variable.Value)}", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            list.Items.Add(row);
        }
    }

    private void Add(object? sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(name.Text)) return; items.RemoveAll(x => string.Equals(x.Name, name.Text.Trim(), StringComparison.OrdinalIgnoreCase)); items.Add(new(name.Text.Trim(), value.Text, secret.IsChecked == true)); name.Clear(); value.Clear(); secret.IsChecked = false; Refresh(); }
    private void Remove(object? sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.Tag is ScenarioVariableDefinition variable) { items.Remove(variable); Refresh(); } }
}
