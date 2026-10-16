using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace AtlasTesterDesktop;

public partial class HomeWindow : Window
{
    public HomeWindow()
    {
        InitializeComponent();
        PageContent.Margin = new Thickness(30);
        ConfigureSidebar();
        SetNavigationState("Home");
        var homeButtons = FindVisualChildren<Button>(HomeContent).ToArray();
        if (homeButtons.Length > 0) homeButtons[^1].Click += Reports_Click;
    }

    private void Reports_Click(object sender, RoutedEventArgs e)
    {
        AlertWindow.Show(this, "Reports", "Run a performance test first to generate a report.");
    }

    private void ConfigureSidebar()
    {
        if (Content is not Grid shell || shell.Children[0] is not Border sidebar || sidebar.Child is not StackPanel nav) return;

        if (nav.Children[0] is TextBlock logo)
        {
            nav.Children.RemoveAt(0);
            nav.Children.Insert(0, new Wpf.Ui.Controls.SymbolIcon
            {
                Symbol = Wpf.Ui.Controls.SymbolRegular.Globe24,
                FontSize = 28,
                Foreground = new SolidColorBrush(Color.FromRgb(85, 181, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 28)
            });
        }

        foreach (var button in nav.Children.OfType<Button>().ToArray())
        {
            var label = button.Content?.ToString() ?? string.Empty;
            if (label.Contains("Theme", StringComparison.OrdinalIgnoreCase))
            {
                button.Visibility = Visibility.Collapsed;
                continue;
            }

            var icon = label.Contains("Home", StringComparison.OrdinalIgnoreCase)
                ? Wpf.Ui.Controls.SymbolRegular.Home32
                : label.Contains("Tests", StringComparison.OrdinalIgnoreCase)
                    ? Wpf.Ui.Controls.SymbolRegular.PlayCircle28
                    : Wpf.Ui.Controls.SymbolRegular.BranchFork24;
            button.Tag = label.Contains("Home", StringComparison.OrdinalIgnoreCase)
                ? "Home"
                : label.Contains("Tests", StringComparison.OrdinalIgnoreCase) ? "Tests" : "Scenarios";
            button.Content = new Wpf.Ui.Controls.SymbolIcon { Symbol = icon, FontSize = 24 };
            button.ToolTip = button.Tag?.ToString() switch
            {
                "Home" => "Home",
                "Tests" => "Performance test",
                "Scenarios" => "Scenario builder",
                _ => null
            };
            button.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, button.ToolTip);
            button.Width = 58;
            button.Height = 58;
            button.Padding = new Thickness(0);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
        }
    }
    private void Home_Click(object sender, RoutedEventArgs e) => ShowHome();
    private void Tests_Click(object sender, RoutedEventArgs e) => ShowPage(new MainWindow());
    private async void Scenarios_Click(object sender, RoutedEventArgs e) => await OpenScenarioBuilderAsync(new Uri("http://localhost:5000/"));
    private void Theme_Click(object sender, RoutedEventArgs e) { }
    private void ShowHome()
    {
        HomeContent.Visibility = Visibility.Visible;
        PageContent.Visibility = Visibility.Collapsed;
        SetNavigationState("Home");
    }
    private void ShowPage(object page)
    {
        HomeContent.Visibility = Visibility.Collapsed;
        PageContent.Visibility = Visibility.Visible;
        SetNavigationState(page is MainWindow ? "Tests" : "Scenarios");
        HideInnerHeader(page);
        PageContent.Background = new SolidColorBrush(Color.FromRgb(17, 21, 27));
        if (page is MainWindow testPage && testPage.Content is Grid testRoot)
        {
            testRoot.Background = PageContent.Background;
        }
        else if (page is ScenarioBuilderWindow scenario && scenario.Content is Grid scenarioRoot)
        {
            scenarioRoot.Margin = new Thickness(0);
            scenarioRoot.Background = PageContent.Background;
            scenario.Background = PageContent.Background;
        }
        var pageLayout = new Grid();
        pageLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        pageLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var pageHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
        pageHeader.Children.Add(new TextBlock
        {
            Text = page is MainWindow ? "ATLAS  /  TESTER" : "ATLAS  /  SCENARIO BUILDER",
            Foreground = new SolidColorBrush(Color.FromRgb(85, 181, 255)),
            FontSize = 14,
            FontWeight = FontWeights.Bold
        });
        if (page is MainWindow)
        {
            pageHeader.Children.Add(new TextBlock
            {
                Text = "Run a performance test",
                Foreground = Brushes.White,
                FontSize = 32,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 4)
            });
            pageHeader.Children.Add(new TextBlock
            {
                Text = "Execute a real HTTP scenario against your selected target",
                Foreground = new SolidColorBrush(Color.FromRgb(174, 179, 192)),
                FontSize = 14
            });
        }
        Grid.SetRow(pageHeader, 0);
        pageLayout.Children.Add(pageHeader);

        var pageHost = new ContentControl { Content = page };
        Grid.SetRow(pageHost, 1);
        pageLayout.Children.Add(pageHost);
        PageContent.Content = pageLayout;
        if (page is FrameworkElement element)
        {
            element.Loaded += PageContent_Loaded;
            Dispatcher.BeginInvoke(() =>
            {
                HideInnerHeader(page);
                MoveScrollBarsLeft(page);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void PageContent_Loaded(object sender, RoutedEventArgs e)
    {
        if (PageContent.Content is object page)
        {
            HideInnerHeader(page);
            MoveScrollBarsLeft(page);
        }
        if (PageContent.Content is FrameworkElement element) element.Loaded -= PageContent_Loaded;
    }

    private void SetNavigationState(string selected)
    {
        if (Content is not Grid shell || shell.Children.Count == 0) return;
        if (shell.Children[0] is not Border sidebar || sidebar.Child is not StackPanel nav) return;
        foreach (var button in nav.Children.OfType<Button>())
        {
            var label = button.Tag?.ToString() ?? button.Content?.ToString() ?? string.Empty;
            var active = label.Contains(selected, StringComparison.OrdinalIgnoreCase);
            button.Background = new SolidColorBrush(active
                ? Color.FromRgb(44, 126, 174)
                : System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
            button.Foreground = new SolidColorBrush(active
                ? System.Windows.Media.Colors.White
                : Color.FromRgb(183, 187, 200));
        }
    }

    private void HideInnerHeader(object page)
    {
        if (page is MainWindow testPage && testPage.Content is Grid root && root.RowDefinitions.Count > 0 && root.Children.Count > 0)
        {
            root.RowDefinitions[0].Height = new GridLength(0);
            root.Children[0].Visibility = Visibility.Collapsed;
        }
        else if (page is ScenarioBuilderWindow scenario && scenario.Content is Grid scenarioRoot && scenarioRoot.RowDefinitions.Count > 0 && scenarioRoot.Children.Count > 0)
        {
            scenarioRoot.RowDefinitions[0].Height = new GridLength(0);
            scenarioRoot.Children[0].Visibility = Visibility.Collapsed;
        }
    }

    private static void MoveScrollBarsLeft(object page)
    {
        if (page is not DependencyObject root) return;
        var template = Application.Current.MainWindow is HomeWindow host
            ? host.Resources["LeftRailScrollViewerTemplate"] as ControlTemplate
            : null;
        foreach (var scrollViewer in FindVisualChildren<ScrollViewer>(root).ToArray())
        {
            if (template is not null) scrollViewer.Template = template;
            scrollViewer.FlowDirection = FlowDirection.LeftToRight;
            scrollViewer.ApplyTemplate();
            if (scrollViewer.Content is FrameworkElement content)
            {
                content.FlowDirection = FlowDirection.LeftToRight;
            }
            UpdateScrollBarSpacing(scrollViewer);
            if (scrollViewer.Tag as string != "spacing-configured")
            {
                scrollViewer.Tag = "spacing-configured";
                scrollViewer.SizeChanged += ScrollViewerLayoutChanged;
                scrollViewer.ScrollChanged += ScrollViewerLayoutChanged;
            }
        }
    }

    private static void ScrollViewerLayoutChanged(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer) UpdateScrollBarSpacing(scrollViewer);
    }

    private static void UpdateScrollBarSpacing(ScrollViewer scrollViewer)
    {
        if (scrollViewer.Content is not FrameworkElement content) return;
        foreach (var scrollBar in FindVisualChildren<ScrollBar>(scrollViewer))
        {
            scrollBar.FlowDirection = FlowDirection.LeftToRight;
        }

        var marginBeforeBottomScrollBar = scrollViewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible ? 10 : 0;
        content.Margin = new Thickness(0, 0, 0, marginBeforeBottomScrollBar);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }
    public async Task OpenScenarioBuilderAsync(Uri target) => ShowPage(await ScenarioBuilderWindow.CreateAsync(target));
}
