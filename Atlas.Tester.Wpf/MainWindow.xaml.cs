using System.Net.Http;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Atlas.Tester.Engine;
using Serilog;

namespace AtlasTesterDesktop;

public partial class MainWindow : UserControl
{
    private readonly TesterRunner runner = new();
    private CancellationTokenSource? cancellation;
    private Stopwatch? runClock;
    private int observedRequests;
    private int successfulRequests;
    private int expectedRequests;
    private Border? reportCard;
    private readonly Dictionary<string, string> savedScenarioPaths = new(StringComparer.OrdinalIgnoreCase);
    private string? latestReportDirectory;
    private readonly ProjectDefinition? project;

    public MainWindow(ProjectDefinition? project = null)
    {
        this.project = project;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}").CreateLogger();
        Log.Information("Tester UI started");
        InitializeComponent();
        HideLegacyCredentialInputs();
        if (project is not null) TargetUrlBox.Text = project.BackendUrl;
        ChartStatus.Text = "Ready to run · results will appear here";
        ReportStatus.Text = "No test has run yet. Reports will be generated after completion.";
        OutputStatus.Text = $"Reports: {ClientDataPaths.ReportsDirectory}";
        AddReportActions();
        AddSandboxAction();
        StopButton.IsEnabled = false;
        ThreadsSlider.ValueChanged += (_, _) => ThreadsValue.Text = $"{ThreadsSlider.Value:0} threads";
        RampUpSlider.ValueChanged += (_, _) => RampUpValue.Text = $"{RampUpSlider.Value:0} seconds";
        RefreshScenarios();
    }


    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Start clicked: target={Target}, scenario={Scenario}", TargetUrlBox.Text, ScenarioBox.SelectedValue);
            if (!AcknowledgeBox.IsChecked.GetValueOrDefault())
            {
                Log.Warning("Performance test start blocked because target acknowledgement was not checked");
                AlertWindow.Show(this, "Test confirmation required", "To start the test, enable the acknowledgement checkbox confirming that this test may put load on the target.", true);
                AcknowledgeBox.Focus();
                return;
            }
            var target = TargetSafety.Validate(TargetUrlBox.Text.Trim(), false);
            cancellation = new CancellationTokenSource();
            SetRunState(true);
            ChartStatus.Text = "Preparing test...";
            ReportStatus.Text = "Loading the selected scenario and validating the target...";
            var selectedItem = ScenarioBox.SelectedItem as System.Windows.Controls.ComboBoxItem; var selectedText = ScenarioBox.SelectedItem?.ToString() ?? ""; var key = selectedItem?.Tag?.ToString() ?? (savedScenarioPaths.TryGetValue(selectedText, out var savedPath) ? $"file:{savedPath}" : "user-portfolio");
            Log.Information("Loading selected scenario: key={Key}", key);
            var scenario = key.StartsWith("file:", StringComparison.Ordinal) ? await LoadSavedScenarioAsync(key[5..], cancellation?.Token ?? CancellationToken.None) : ScenarioCatalog.All.Single(x => x.Key == key);
            Log.Information("Selected scenario loaded: key={Key}, steps={StepCount}", scenario.Key, scenario.Steps.Count);
            ScenarioSummary.Text = $"{scenario.Steps.Count} step{(scenario.Steps.Count == 1 ? "" : "s")} · configure load and run when ready";
            if (!int.TryParse(IterationsBox.Text, out var iterations)) throw new InvalidOperationException("Iterations must be a number.");
            ReportTable.Visibility = Visibility.Collapsed;
            ChartStatus.Text = "Loading OpenAPI contract..."; ReportStatus.Text = "Validating the selected scenario against the target API.";
            using var client = new HttpClient { BaseAddress = target.Target, Timeout = TimeSpan.FromSeconds(30) };
            var openApi = await OpenApiCatalog.LoadAsync(client, project?.OpenApiPath ?? "/openapi/v1.json", cancellation!.Token);
            openApi.Validate(scenario);
            ChartStatus.Text = "Running live HTTP requests"; ReportStatus.Text = "Connected to target; collecting measurements.";
            var options = new TesterOptions((int)ThreadsSlider.Value, iterations, TimeSpan.FromSeconds(RampUpSlider.Value), client.Timeout);
            expectedRequests = options.Threads * options.Iterations * scenario.Steps.Count; observedRequests = 0; successfulRequests = 0; runClock = Stopwatch.StartNew();
            RequestsValue.Text = "0"; SuccessValue.Text = "0.00%"; LatencyValue.Text = "—"; MockProgress.Value = 0; ChartStatus.Text = $"Running {scenario.DisplayName} · 0 / {expectedRequests} requests";
            var measurements = await runner.RunScenarioAsync(options, ct => new HttpScenarioExecutor(client, scenario, scenario.Variables ?? new Dictionary<string, string>()).RunAsync(ct), new Progress<RequestMeasurement>(UpdateLiveMetrics), cancellation!.Token);
            var rows = measurements.GroupBy(x => new { x.Scenario, x.Endpoint }).Select(group => { var values = group.Select(x => x.Duration).OrderBy(x => x).ToArray(); return new ReportRow(group.Key.Scenario, group.Key.Endpoint, values.Length, group.Count(x => x.Succeeded), group.Count(x => !x.Succeeded), values.Average(x => x.TotalMilliseconds), values[Math.Min(values.Length - 1, (int)Math.Ceiling(values.Length * .95) - 1)].TotalMilliseconds); }).ToArray();
            var output = Path.Combine(ClientDataPaths.ReportsDirectory, "latest"); Directory.CreateDirectory(output);
            var secretValues = (scenario.SecretVariableNames ?? []).Select(name => scenario.Variables?.GetValueOrDefault(name)).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
            var details = measurements.Select(x => new RequestReportDetail(x.Scenario, x.Endpoint, x.Method, MaskSecrets(x.RequestUrl, secretValues), MaskSecrets(x.RequestBody, secretValues), x.StatusCode, MaskSecrets(x.ResponseBody, secretValues), MaskSecrets(x.Error, secretValues), x.Duration.TotalMilliseconds, x.Succeeded, x.CapturedValues?.ToDictionary(pair => pair.Key, pair => MaskSecrets(pair.Value, secretValues) ?? string.Empty))).ToArray();
            var report = new TesterReport(DateTimeOffset.UtcNow, rows, details);
            latestReportDirectory = output;
            OpenReportButton?.SetValue(IsEnabledProperty, true);
            (OpenReportButton?.Tag as Button)?.SetValue(IsEnabledProperty, true);
            await TesterReportWriter.WriteJsonAsync(report, Path.Combine(output, "report.json")); await TesterReportWriter.WriteCsvAsync(report, Path.Combine(output, "report.csv")); await TesterReportWriter.WriteRequestCsvAsync(report, Path.Combine(output, "requests.csv")); await TesterReportWriter.WriteHtmlAsync(report, Path.Combine(output, "report.html"));
            var succeeded = measurements.Count(x => x.Succeeded); var failed = measurements.Count - succeeded; var elapsed = runClock?.Elapsed.TotalSeconds ?? 0; var orderedLatencies = measurements.Select(x => x.Duration).OrderBy(x => x).ToArray(); var p95 = orderedLatencies[Math.Min(orderedLatencies.Length - 1, (int)Math.Ceiling(orderedLatencies.Length * .95) - 1)]; var errors = measurements.Where(x => !x.Succeeded).GroupBy(x => x.StatusCode?.ToString() ?? "network").OrderByDescending(x => x.Count()).Select(x => $"{x.Key}: {x.Count()}");
            RequestsValue.Text = $"{(elapsed <= 0 ? 0 : measurements.Count / elapsed):0.0}"; RequestsHint.Text = $"{measurements.Count} total requests"; SuccessValue.Text = $"{(double)succeeded / measurements.Count * 100:0.00}%"; SuccessHint.Text = $"{succeeded} succeeded · {failed} failed"; LatencyValue.Text = $"{p95.TotalMilliseconds:0} ms"; LatencyHint.Text = "P95 response time";
            ChartStatus.Text = "Real HTTP scenario completed"; ReportStatus.Text = "Detailed result"; OutputStatus.Text = $"Reports: {output} · request/response details in JSON, CSV and HTML"; ShowReportCard(scenario.DisplayName, measurements.Count, succeeded, failed, elapsed, p95, errors);
        }
        catch (OperationCanceledException) { Log.Warning("Performance test cancelled"); ChartStatus.Text = "Test stopped · cancelled by user"; ReportStatus.Text = "The running HTTP test was cancelled."; }
        catch (Exception ex) { Log.Error(ex, "Performance test failed"); ChartStatus.Text = "Test failed"; ReportStatus.Text = ex.Message; }
        finally { cancellation?.Dispose(); cancellation = null; SetRunState(false); }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) { Log.Information("Stop clicked"); cancellation?.Cancel(); }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) { }

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { }

    private void AddReportActions()
    {
        if (OutputStatus.Parent is not Panel panel) return;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var reportButton = new Button { ToolTip = "Open HTML report", Width = 42, Height = 42, Padding = new Thickness(0), Background = new SolidColorBrush(Color.FromRgb(234, 246, 255)), Foreground = new SolidColorBrush(Color.FromRgb(8, 122, 190)), IsEnabled = false };
        reportButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Open HTML report");
        reportButton.Content = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.DocumentText24 };
        reportButton.Click += (_, _) => OpenLatestReport();
        var folderButton = new Button { ToolTip = "Open report folder", Width = 42, Height = 42, Padding = new Thickness(0), Background = new SolidColorBrush(Color.FromRgb(234, 246, 255)), Foreground = new SolidColorBrush(Color.FromRgb(8, 122, 190)), IsEnabled = false };
        folderButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Open report folder");
        folderButton.Content = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.FolderOpen24 };
        folderButton.Click += (_, _) => OpenLatestReportFolder();
        actions.Children.Add(reportButton); actions.Children.Add(folderButton); panel.Children.Add(actions);
        reportButton.Tag = folderButton;
        OpenReportButton = reportButton;
    }

    private void AddSandboxAction()
    {
        if (StartButton.Parent is not Panel panel) return;
        var sandboxButton = new Button { Content = "Sandbox", ToolTip = "Review the isolated sandbox plan", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        sandboxButton.Click += (_, _) =>
        {
            if (project is null)
            {
                AlertWindow.Show(this, "Sandbox project required", "Select a project before preparing an isolated sandbox.", true);
                return;
            }
            new SandboxPreparationWindow(project).ShowDialog();
        };
        panel.Children.Insert(0, sandboxButton);
    }

    private void HideLegacyCredentialInputs()
    {
        if (EmailBox.Parent is not Panel panel) return;
        var emailIndex = panel.Children.IndexOf(EmailBox);
        var passwordIndex = panel.Children.IndexOf(PasswordBox);
        if (emailIndex > 0) panel.Children[emailIndex - 1].Visibility = Visibility.Collapsed;
        if (passwordIndex > 0) panel.Children[passwordIndex - 1].Visibility = Visibility.Collapsed;
        EmailBox.Visibility = Visibility.Collapsed; PasswordBox.Visibility = Visibility.Collapsed;
    }

    private Button? OpenReportButton { get; set; }

    private void OpenLatestReport()
    {
        if (latestReportDirectory is null) return;
        Process.Start(new ProcessStartInfo(Path.Combine(latestReportDirectory, "report.html")) { UseShellExecute = true });
    }

    private void OpenLatestReportFolder()
    {
        if (latestReportDirectory is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", latestReportDirectory) { UseShellExecute = true });
    }

    private void SetRunState(bool running)
    {
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        TargetUrlBox.IsEnabled = !running;
        ScenarioBox.IsEnabled = !running;
        ThreadsSlider.IsEnabled = !running;
        RampUpSlider.IsEnabled = !running;
        IterationsBox.IsEnabled = !running;
        AcknowledgeBox.IsEnabled = !running;
    }

    private void RefreshScenarios_Click(object sender, RoutedEventArgs e) => RefreshScenarios();

    private void ScenarioBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SelectedScenarioLabel is null) return;
        var item = ScenarioBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
        var rawText = ScenarioBox.SelectedItem?.ToString() ?? string.Empty;
        var separatorIndex = rawText.LastIndexOf(": ", StringComparison.Ordinal);
        var text = separatorIndex >= 0 ? rawText[(separatorIndex + 2)..] : rawText;
        if (item?.Content is not null)
        {
            text = item.Content.ToString() ?? text;
        }
        SelectedScenarioLabel.Text = $"Active: {text ?? "None"}";
        ScenarioSummary.Text = text is null ? "Choose a scenario to see its test plan" : "Scenario selected · configure load and run when ready";
        Log.Information("Scenario selection changed: content={Content}, tag={Tag}, type={Type}, selectedIndex={Index}", text, item?.Tag, ScenarioBox.SelectedItem?.GetType().FullName, ScenarioBox.SelectedIndex);
    }

    private void RefreshScenarios()
    {
        var store = new ScenarioStore(); var directory = store.DirectoryPath; Directory.CreateDirectory(directory);
        Log.Information("Scenario refresh started: directory={Directory}, comboItemsBefore={Count}", directory, ScenarioBox.Items.Count);
        foreach (var item in savedScenarioPaths.Keys.ToArray()) ScenarioBox.Items.Remove(item); savedScenarioPaths.Clear();
        var saved = store.List();
        foreach (var item in saved) Log.Information("Scenario file discovered by engine: {File}, bytes={Bytes}", item.Path, item.SizeBytes);
        foreach (var item in saved.Reverse()) { var display = $"Saved · {item.Name}"; savedScenarioPaths[display] = item.Path; ScenarioBox.Items.Add(display); }
        if (saved.Count > 0) ScenarioBox.SelectedItem = $"Saved · {saved[0].Name}";
        ScenarioBox.UpdateLayout();
        var selectedDisplay = ScenarioBox.SelectedItem?.ToString() ?? string.Empty;
        var selectedSeparator = selectedDisplay.LastIndexOf(": ", StringComparison.Ordinal);
        if (selectedSeparator >= 0) selectedDisplay = selectedDisplay[(selectedSeparator + 2)..];
        SelectedScenarioLabel.Text = $"Active: {(string.IsNullOrWhiteSpace(selectedDisplay) ? "None" : selectedDisplay)}";
        Log.Information("Saved scenarios refreshed: files={FileCount}, comboItemsAfter={Count}, selected={Selected}, selectedIndex={Index}", saved.Count, ScenarioBox.Items.Count, ScenarioBox.SelectedItem, ScenarioBox.SelectedIndex);
    }

    private static async Task<TesterScenario> LoadSavedScenarioAsync(string path, CancellationToken cancellationToken)
    {
        Log.Information("Loading saved scenario file: {Path}", path);
        var definition = await new ScenarioStore(Path.GetDirectoryName(path)!).LoadAsync(path, cancellationToken);
        Log.Information("Saved scenario file loaded: name={Name}, steps={StepCount}", definition.Name, definition.Steps.Count);
        return new TesterScenario($"saved:{definition.Name}", definition.Name, definition.Steps.Select(x => new ScenarioStep(x.DisplayName, x.Method, x.Path, x.RequiresAuthentication, x.Request, x.Captures, x.Assertions)).ToArray(), definition.Variables.ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase), definition.Variables.Where(x => x.IsSecret).Select(x => x.Name).ToArray());
    }

    private static string? MaskSecrets(string? value, IEnumerable<string> secretValues)
    {
        if (string.IsNullOrEmpty(value)) return value;
        foreach (var secretValue in secretValues) value = value.Replace(secretValue, "••••••", StringComparison.Ordinal);
        return value;
    }

    private async void ScenarioBuilder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Window.GetWindow(this) is not HomeWindow host) return;
            if (project is null) throw new InvalidOperationException("Select a project before opening Scenario Builder.");
            await host.OpenScenarioBuilderAsync(project);
        }
        catch (Exception ex) { Log.Error(ex, "Scenario builder failed"); AlertWindow.Show(this, "Scenario builder error", ex.Message, true); }
    }

    private void UpdateLiveMetrics(RequestMeasurement measurement)
    {
        observedRequests++; if (measurement.Succeeded) successfulRequests++;
        var elapsed = runClock?.Elapsed.TotalSeconds ?? 0;
        RequestsValue.Text = elapsed <= 0 ? observedRequests.ToString() : $"{observedRequests / elapsed:0.0}";
        SuccessValue.Text = $"{(double)successfulRequests / observedRequests * 100:0.00}%";
        MockProgress.Value = expectedRequests == 0 ? 0 : Math.Min(100, observedRequests * 100d / expectedRequests);
        RequestsHint.Text = $"Latest: {(measurement.Succeeded ? "success" : "error")} {measurement.StatusCode?.ToString() ?? "network failure"}";
        LatencyValue.Text = $"{measurement.Duration.TotalMilliseconds:0} ms"; LatencyHint.Text = "Latest response time"; ChartStatus.Text = $"{measurement.Scenario}: {measurement.Endpoint}";
    }

    private void ShowReportCard(string scenario, int total, int succeeded, int failed, double elapsed, TimeSpan p95, IEnumerable<string> errors)
    {
        if (ReportStatus.Parent is not Panel panel) return;
        if (reportCard is not null) panel.Children.Remove(reportCard);
        var rate = total == 0 ? 0 : succeeded * 100d / total;
        var accent = rate >= 95 ? Color.FromRgb(19, 129, 98) : rate >= 80 ? Color.FromRgb(218, 145, 35) : Color.FromRgb(192, 75, 91);
        reportCard = new Border { Margin = new Thickness(0, 12, 0, 8), Padding = new Thickness(16), CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromRgb(248, 252, 255)), BorderBrush = new SolidColorBrush(Color.FromRgb(222, 235, 245)), BorderThickness = new Thickness(1) };
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = "TEST RESULT", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(20, 152, 232)), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
        content.Children.Add(new TextBlock { Text = scenario, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(16, 36, 62)), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 14) });
        var table = new Grid(); table.ColumnDefinitions.Add(new ColumnDefinition()); table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AddMetric(table, "Total requests", total.ToString("N0"), 0, Colors.DarkSlateGray); AddMetric(table, "Succeeded", succeeded.ToString("N0"), 1, Color.FromRgb(19, 129, 98)); AddMetric(table, "Failed", failed.ToString("N0"), 2, Color.FromRgb(192, 75, 91)); AddMetric(table, "Success rate", $"{rate:0.00}%", 3, accent); AddMetric(table, "Throughput", $"{(elapsed <= 0 ? 0 : total / elapsed):0.0} req/s", 4, Colors.DarkSlateGray); AddMetric(table, "Duration", $"{elapsed:0.0}s", 5, Colors.DarkSlateGray); AddMetric(table, "P95 latency", $"{p95.TotalMilliseconds:0} ms", 6, Colors.DarkSlateGray);
        content.Children.Add(table);
        var conclusion = rate >= 95 ? "Excellent result: the scenario completed with a healthy success rate" : rate >= 80 ? "Needs attention: most requests succeeded, but the failed requests should be investigated" : "Unhealthy result: the failure rate is high and the scenario/data setup needs investigation";
        content.Children.Add(new TextBlock { Text = conclusion, TextWrapping = TextWrapping.Wrap, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(accent), Margin = new Thickness(0, 16, 0, 6) });
        var errorText = string.Join("  ·  ", errors); var errorSummary = string.IsNullOrWhiteSpace(errorText) ? "No errors recorded" : $"Error breakdown: {errorText}"; var errorDetails = "Error breakdown groups failures by HTTP status code. For example, 400 usually means an invalid request and 409 means a conflict with the current data state."; var errorBlock = new TextBlock { Text = errorSummary, TextWrapping = TextWrapping.Wrap, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(192, 75, 91)), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = errorDetails }; errorBlock.MouseLeftButtonUp += (_, _) => AlertWindow.Show(this, "Error breakdown", errorDetails); content.Children.Add(errorBlock);
        reportCard.Child = content; panel.Children.Insert(panel.Children.IndexOf(ReportStatus) + 1, reportCard);
    }

    private static void AddMetric(Grid grid, string label, string value, int row, Color color)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var explanation = label switch { "Total requests" => "تعداد کل درخواست‌های ارسال‌شده به بک‌اند", "Succeeded" => "تعداد درخواست‌های موفق با پاسخ 2xx", "Failed" => "تعداد درخواست‌های ناموفق یا دارای خطای شبکه", "Success rate" => "درصد درخواست‌های موفق از کل درخواست‌ها", "Throughput" => "میانگین تعداد درخواست‌های پردازش‌شده در هر ثانیه", "Duration" => "مدت زمان کامل اجرای سناریو از شروع تا پایان", "P95 latency" => "زمانی که 95 درصد درخواست‌ها کمتر یا مساوی آن پاسخ گرفته‌اند", _ => "" };
        var separator = new Border { Background = Brushes.Transparent, BorderBrush = new SolidColorBrush(Color.FromRgb(225, 236, 244)), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 2, 0, 2) };
        Grid.SetRow(separator, row); Grid.SetColumnSpan(separator, 2); grid.Children.Add(separator);
        var labelText = new TextBlock { Text = $"{label}  [{explanation}]", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(86, 112, 135)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 12, 3) };
        var valueText = new TextBlock { Text = value, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(color), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 3, 0, 3) };
        Grid.SetRow(labelText, row); Grid.SetColumn(labelText, 0); Grid.SetRow(valueText, row); Grid.SetColumn(valueText, 1); grid.Children.Add(labelText); grid.Children.Add(valueText);
    }
}
