using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Atlas.Tester.Engine;

namespace AtlasTesterDesktop;

internal sealed class SandboxPreparationWindow : Window
{
    private readonly ProjectDefinition project;
    private readonly TextBlock dockerStatus = new() { Foreground = new SolidColorBrush(Color.FromRgb(174, 179, 192)), TextWrapping = TextWrapping.Wrap };
    private readonly TextBox errorDetails = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 120, Visibility = Visibility.Collapsed };
    public SandboxRuntime? Runtime { get; private set; }

    public SandboxPreparationWindow(ProjectDefinition project)
    {
        this.project = project; Title = "Isolated sandbox"; Width = 680; Height = 560; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(41, 42, 56));
        Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        var root = new StackPanel { Margin = new Thickness(28) };
        root.Children.Add(new TextBlock { Text = "ISOLATED SANDBOX", Foreground = new SolidColorBrush(Color.FromRgb(85, 181, 255)), FontSize = 18, FontWeight = FontWeights.Bold });
        root.Children.Add(new TextBlock { Text = "Nothing starts until you select Start isolated backend. After it is ready, return to Test and run your scenario.", Foreground = new SolidColorBrush(Color.FromRgb(174, 179, 192)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 18) });
        try
        {
            var plan = SandboxPlanner.Create(project);
            AddInfo(root, "BACKEND", plan.BackendProjectPath);
            AddInfo(root, "START COMMAND", plan.StartCommand);
            AddInfo(root, "TARGET", plan.Target.ToString());
            AddInfo(root, "NEXT RUN NETWORK", plan.NetworkName);
            root.Children.Add(new TextBlock { Text = "ALLOWED DEPENDENCIES", Foreground = new SolidColorBrush(Color.FromRgb(174, 179, 192)), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 14, 0, 5) });
            foreach (var dependency in plan.Dependencies) root.Children.Add(new TextBlock { Text = $"• {dependency.Name} — {dependency.Purpose}", Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });
        }
        catch (Exception ex)
        {
            root.Children.Add(new TextBlock { Text = ex.Message, Foreground = new SolidColorBrush(Color.FromRgb(230, 106, 106)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) });
        }
        dockerStatus.Text = "Docker has not been checked."; dockerStatus.Margin = new Thickness(0, 18, 0, 8); root.Children.Add(dockerStatus);
        errorDetails.Background = new SolidColorBrush(Color.FromRgb(31, 33, 44)); errorDetails.Foreground = Brushes.White; errorDetails.BorderBrush = new SolidColorBrush(Color.FromRgb(129, 75, 81)); errorDetails.Padding = new Thickness(8); root.Children.Add(errorDetails);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var check = new Button { Content = "Check Docker", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) }; check.Click += CheckDockerAsync;
        var start = new Button { Content = "Start isolated backend", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) }; start.Click += StartSandboxAsync;
        var copy = new Button { Content = "Copy error", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) }; copy.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(errorDetails.Text)) Clipboard.SetText(errorDetails.Text); };
        var close = new Button { Content = "Close", Padding = new Thickness(14, 8, 14, 8) }; close.Click += (_, _) => Close(); actions.Children.Add(check); actions.Children.Add(start); actions.Children.Add(copy); actions.Children.Add(close); root.Children.Add(actions);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async void StartSandboxAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            var run = SandboxPlanner.Create(project);
            Runtime = new SandboxRuntime(SandboxRuntimePlan.Create(run, run.Target.Port, "host-process", "", "IAM__ConnectionString"));
            dockerStatus.Text = "Starting isolated PostgreSQL and backend...";
            await Runtime.StartAsync(); await Runtime.StartBackendAsync(run.StartCommand, new Dictionary<string, string> { ["ASPNETCORE_URLS"] = run.Target.ToString().TrimEnd('/') }); await Runtime.WaitForReadyAsync(project.OpenApiPath, TimeSpan.FromSeconds(60));
            dockerStatus.Foreground = new SolidColorBrush(Color.FromRgb(90, 191, 133)); dockerStatus.Text = "Isolated backend is ready."; DialogResult = true;
        }
        catch (Exception ex) { if (Runtime is not null) await Runtime.DisposeAsync(); Runtime = null; ShowError(ex); }
    }

    private static void AddInfo(Panel root, string label, string value)
    {
        root.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(174, 179, 192)), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 7, 0, 2) });
        root.Children.Add(new TextBlock { Text = value, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
    }

    private void ShowError(Exception error)
    {
        dockerStatus.Foreground = new SolidColorBrush(Color.FromRgb(230, 106, 106)); dockerStatus.Text = "Sandbox startup failed. Review or copy the details below.";
        errorDetails.Text = error.ToString(); errorDetails.Visibility = Visibility.Visible;
    }

    private async void CheckDockerAsync(object? sender, RoutedEventArgs e)
    {
        dockerStatus.Text = "Checking Docker...";
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "version --format \"{{.Server.Version}}\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true });
            if (process is null) throw new InvalidOperationException("Docker could not be started.");
            var output = await process.StandardOutput.ReadToEndAsync(); var error = await process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
            dockerStatus.Foreground = new SolidColorBrush(process.ExitCode == 0 ? Color.FromRgb(90, 191, 133) : Color.FromRgb(230, 106, 106));
            dockerStatus.Text = process.ExitCode == 0 ? $"Docker is ready (server {output.Trim()})." : $"Docker is unavailable: {error.Trim()}";
        }
        catch (Exception ex)
        {
            dockerStatus.Foreground = new SolidColorBrush(Color.FromRgb(230, 106, 106));
            dockerStatus.Text = $"Docker is unavailable: {ex.Message}";
        }
    }
}
