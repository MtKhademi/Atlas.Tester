using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Net.Http;
using Atlas.Tester.Engine;
using Serilog;

namespace AtlasTesterDesktop;

public partial class HomeWindow : Window
{
    private readonly ProjectStore projectStore = new();
    private ProjectDefinition? activeProject;
    private ComboBox? projectSelector;
    private TextBlock? projectDetails;
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

        var projectButton = new Button { Content = "⌘\nProjects", Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(183, 187, 200)), BorderThickness = new Thickness(0), Height = 58, Margin = new Thickness(0, 0, 0, 10), ToolTip = "Projects" };
        projectButton.Click += Projects_Click;
        nav.Children.Insert(2, projectButton);

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
                : label.Contains("Projects", StringComparison.OrdinalIgnoreCase)
                    ? Wpf.Ui.Controls.SymbolRegular.Folder24
                : label.Contains("Tests", StringComparison.OrdinalIgnoreCase)
                    ? Wpf.Ui.Controls.SymbolRegular.PlayCircle28
                    : Wpf.Ui.Controls.SymbolRegular.BranchFork24;
            button.Tag = label.Contains("Home", StringComparison.OrdinalIgnoreCase)
                ? "Home"
                : label.Contains("Projects", StringComparison.OrdinalIgnoreCase) ? "Projects"
                : label.Contains("Tests", StringComparison.OrdinalIgnoreCase) ? "Tests" : "Scenarios";
            button.Content = new Wpf.Ui.Controls.SymbolIcon { Symbol = icon, FontSize = 24 };
            button.ToolTip = button.Tag?.ToString() switch
            {
                "Home" => "Home",
                "Projects" => "Projects",
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
    private void Projects_Click(object sender, RoutedEventArgs e) => ShowProjects();
    private void Tests_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureProjectSelected()) return;
        ShowPage(new MainWindow(activeProject!));
    }

    private async void Scenarios_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureProjectSelected()) return;
        await OpenScenarioBuilderAsync(activeProject!);
    }

    private bool EnsureProjectSelected()
    {
        if (activeProject is not null) return true;
        AlertWindow.Show(this, "Select a project", "Choose an active project from Projects before opening Tests or Scenario Builder.", true);
        ShowProjects();
        return false;
    }
    private void Theme_Click(object sender, RoutedEventArgs e) { }
    private void ShowHome()
    {
        HomeContent.Visibility = Visibility.Visible;
        PageContent.Visibility = Visibility.Collapsed;
        SetNavigationState("Home");
    }

    private void ShowProjects()
    {
        HomeContent.Visibility = Visibility.Collapsed;
        PageContent.Visibility = Visibility.Visible;
        SetNavigationState("Projects");
        var root = new StackPanel { Margin = new Thickness(30) };
        root.Children.Add(new TextBlock { Text = "ATLAS  /  PROJECTS", Foreground = new SolidColorBrush(Color.FromRgb(85, 181, 255)), FontSize = 14, FontWeight = FontWeights.Bold });
        root.Children.Add(new TextBlock { Text = "Project workspaces", Foreground = Brushes.White, FontSize = 32, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) });
        root.Children.Add(new TextBlock { Text = "Manage backend targets, API catalogs and test assets per project", Foreground = new SolidColorBrush(Color.FromRgb(174, 179, 192)), FontSize = 14, Margin = new Thickness(0, 0, 0, 22) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 14) };
        var add = new Button { Content = "+ Add project", Padding = new Thickness(14, 8, 14, 8), Background = new SolidColorBrush(Color.FromRgb(33, 111, 164)), Foreground = Brushes.White, BorderThickness = new Thickness(0) }; add.Click += AddProject_Click; actions.Children.Add(add); root.Children.Add(actions);
        var table = new Grid { Background = new SolidColorBrush(Color.FromRgb(48, 50, 64)), Margin = new Thickness(0, 0, 0, 12) }; table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }); table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) }); table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) }); table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        AddTableHeader(table);
        foreach (var p in projectStore.List()) AddProjectRow(table, p, table.RowDefinitions.Count);
        if (table.RowDefinitions.Count == 1) { table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); table.Children.Add(new TextBlock { Text = "No projects yet. Add your first backend project.", Foreground = new SolidColorBrush(Color.FromRgb(174, 179, 192)), Padding = new Thickness(18) }); Grid.SetRow(table.Children[^1], 1); Grid.SetColumnSpan(table.Children[^1], 4); }
        root.Children.Add(table); PageContent.Content = root;
    }

    private static void AddTableHeader(Grid table)
    {
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var item in new[] { "PROJECT", "BACKEND", "OPENAPI", "" }) { var text = new TextBlock { Text = item, Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 226)), FontSize = 10, FontWeight = FontWeights.Bold, Padding = new Thickness(18, 12, 18, 12), Background = new SolidColorBrush(Color.FromRgb(43, 45, 58)) }; Grid.SetRow(text, 0); Grid.SetColumn(text, Array.IndexOf(new[] { "PROJECT", "BACKEND", "OPENAPI", "" }, item)); table.Children.Add(text); }
    }

    private void AddProjectRow(Grid table, ProjectDefinition project, int row)
    {
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var selected = activeProject?.Id == project.Id;
        var rowBackground = new Border { Background = new SolidColorBrush(selected ? Color.FromRgb(48, 105, 83) : Color.FromRgb(57, 59, 74)), BorderBrush = new SolidColorBrush(Color.FromRgb(70, 73, 88)), BorderThickness = new Thickness(0, 0, 0, 1), Cursor = System.Windows.Input.Cursors.Hand };
        Grid.SetRow(rowBackground, row); Grid.SetColumnSpan(rowBackground, 4); table.Children.Add(rowBackground);
        rowBackground.MouseEnter += (_, _) => { if (activeProject?.Id != project.Id) rowBackground.Background = new SolidColorBrush(Color.FromRgb(68, 72, 89)); };
        rowBackground.MouseLeave += (_, _) => { rowBackground.Background = new SolidColorBrush(activeProject?.Id == project.Id ? Color.FromRgb(48, 105, 83) : Color.FromRgb(57, 59, 74)); };
        rowBackground.MouseLeftButtonUp += (_, _) => { activeProject = project; ShowProjects(); };
        var values = new[] { project.Name, project.BackendUrl, project.OpenApiPath };
        for (var i = 0; i < values.Length; i++) { var text = new TextBlock { Text = values[i], Foreground = Brushes.White, Padding = new Thickness(18, 14, 18, 14), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false }; Grid.SetRow(text, row); Grid.SetColumn(text, i); table.Children.Add(text); }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(CreateIconButton(Wpf.Ui.Controls.SymbolRegular.Play24, "Use project", (_, _) => { activeProject = project; ShowProjects(); }));
        actions.Children.Add(CreateIconButton(Wpf.Ui.Controls.SymbolRegular.Edit24, "Edit project", async (_, _) => await EditProjectAsync(project)));
        actions.Children.Add(CreateIconButton(Wpf.Ui.Controls.SymbolRegular.Delete24, "Delete project", async (_, _) => await DeleteProjectAsync(project), true));
        Grid.SetRow(actions, row); Grid.SetColumn(actions, 3); table.Children.Add(actions);
    }

    private static Button CreateIconButton(Wpf.Ui.Controls.SymbolRegular symbol, string tooltip, RoutedEventHandler click, bool danger = false)
    {
        var button = new Button { Content = new Wpf.Ui.Controls.SymbolIcon { Symbol = symbol, FontSize = 18 }, ToolTip = tooltip, Padding = new Thickness(8), Margin = new Thickness(2), Background = Brushes.Transparent, Foreground = new SolidColorBrush(danger ? Color.FromRgb(220, 105, 115) : Color.FromRgb(174, 210, 235)), BorderThickness = new Thickness(0) }; button.Click += click; return button;
    }

    private async Task EditProjectAsync(ProjectDefinition project)
    {
        var dialog = new ProjectDialog(this, project); if (dialog.ShowDialog() != true) return;
        var loading = new LoadingWindow(this, "Updating project", "Refreshing the saved OpenAPI catalog for this project...");
        loading.Show();
        try
        {
            await Task.Yield();
            using var client = new HttpClient { BaseAddress = new Uri(dialog.Project.BackendUrl), Timeout = TimeSpan.FromSeconds(15) };
            var catalog = await OpenApiCatalog.LoadAsync(client, dialog.Project.OpenApiPath);
            await new ProjectCatalogStore().SaveAsync(dialog.Project.Id, catalog);
            await projectStore.SaveAsync(dialog.Project);
            if (activeProject?.Id == project.Id) activeProject = dialog.Project;
            ShowProjects();
        }
        catch (Exception ex)
        {
            AlertWindow.Show(this, "Project update error", ex.Message, true);
        }
        finally
        {
            loading.Close();
        }
    }

    private async Task DeleteProjectAsync(ProjectDefinition project)
    {
        if (!AlertWindow.Confirm(this, "Delete project", $"Delete project '{project.Name}'? This also removes its saved API catalog.", true)) return;
        await projectStore.DeleteAsync(project.Id); if (activeProject?.Id == project.Id) activeProject = null; ShowProjects();
    }
    private void ShowPage(object page)
    {
        DisposeVisibleSandboxes();
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

        var section = page is MainWindow ? "TEST" : "SCENARIO";
        var projectName = activeProject?.Name ?? "NO PROJECT";
        var pageHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
        var breadcrumb = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold };
        breadcrumb.Inlines.Add(new System.Windows.Documents.Run($"ATLAS  /  {section}  /  ") { Foreground = new SolidColorBrush(Color.FromRgb(85, 181, 255)) });
        breadcrumb.Inlines.Add(new System.Windows.Documents.Run(projectName) { Foreground = new SolidColorBrush(Color.FromRgb(220, 105, 115)) });
        pageHeader.Children.Add(breadcrumb);
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

    private void DisposeVisibleSandboxes()
    {
        foreach (var testPage in FindVisualChildren<MainWindow>(PageContent).ToArray())
        {
            try { testPage.DisposeSandboxAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { Log.Error(ex, "Sandbox cleanup failed while changing project page"); }
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
    public async Task OpenScenarioBuilderAsync(ProjectDefinition project)
    {
        var loading = new LoadingWindow(this, "Loading scenario builder", "Preparing the saved API catalog for this project...");
        loading.Show();
        try
        {
            await Task.Yield();
            ShowPage(await ScenarioBuilderWindow.CreateAsync(project));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Scenario builder failed for project {ProjectId}", project.Id);
            AlertWindow.Show(this, "OpenAPI catalog unavailable", ex.Message, true);
        }
        finally
        {
            loading.Close();
        }
    }

    private void SetupProjectsPanel()
    {
        if (HomeContent.Content is not StackPanel home) return;
        var projects = projectStore.List(); activeProject = projects.FirstOrDefault();
        var panel = new Border { Background = new SolidColorBrush(Color.FromRgb(53, 55, 70)), BorderBrush = new SolidColorBrush(Color.FromRgb(69, 72, 88)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 18) };
        var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel(); header.Children.Add(new TextBlock { Text = "ACTIVE PROJECT", Foreground = new SolidColorBrush(Color.FromRgb(174, 179, 192)), FontSize = 11, FontWeight = FontWeights.Bold });
        var add = new Button { Content = "+ Add project", Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(33, 111, 164)), BorderThickness = new Thickness(0), Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Right }; add.Click += AddProject_Click; DockPanel.SetDock(add, Dock.Right); header.Children.Insert(0, add); Grid.SetRow(header, 0); layout.Children.Add(header);
        projectSelector = new ComboBox { ItemsSource = projects, DisplayMemberPath = nameof(ProjectDefinition.Name), SelectedItem = activeProject, Margin = new Thickness(0, 12, 0, 6) }; projectSelector.SelectionChanged += (_, _) => { activeProject = projectSelector.SelectedItem as ProjectDefinition; UpdateProjectDetails(); }; Grid.SetRow(projectSelector, 1); layout.Children.Add(projectSelector);
        projectDetails = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(216, 220, 229)), FontSize = 12, TextWrapping = TextWrapping.Wrap }; Grid.SetRow(projectDetails, 2); layout.Children.Add(projectDetails); panel.Child = layout; home.Children.Insert(4, panel); UpdateProjectDetails();
    }

    private void UpdateProjectDetails() => projectDetails!.Text = activeProject is null ? "No project yet. Add a backend project to start." : $"{activeProject.BackendUrl}{activeProject.OpenApiPath}\nCatalog and scenarios will use this project.";

    private async void AddProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ProjectDialog(this);
            if (dialog.ShowDialog() != true) return;

            var loading = new LoadingWindow(this, "Setting up project", "Connecting to the API and saving its OpenAPI catalog for fast future access...");
            loading.Show();
            try
            {
                await Task.Yield();
                using var client = new HttpClient { BaseAddress = new Uri(dialog.Project.BackendUrl), Timeout = TimeSpan.FromSeconds(15) };
                var catalog = await OpenApiCatalog.LoadAsync(client, dialog.Project.OpenApiPath);
                await new ProjectCatalogStore().SaveAsync(dialog.Project.Id, catalog);
                await projectStore.SaveAsync(dialog.Project);
                Log.Information("Project initialized: project={ProjectId}, operations={OperationCount}", dialog.Project.Id, catalog.Operations.Count);
            }
            finally
            {
                loading.Close();
            }
            activeProject = dialog.Project;

            if (projectSelector is not null)
            {
                projectSelector.ItemsSource = projectStore.List();
                projectSelector.SelectedItem = activeProject;
                UpdateProjectDetails();
            }

            if (PageContent.Visibility == Visibility.Visible && activeProject is not null)
                ShowProjects();
        }
        catch (Exception ex)
        {
            AlertWindow.Show(this, "Project error", ex.Message, true);
        }
    }
}
