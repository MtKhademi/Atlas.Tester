using Atlas.Tester.Engine;

namespace Atlas.Tester.Engine.Tests;

public sealed class TesterEngineTests
{
    [Fact]
    public void Rejects_remote_targets_without_explicit_opt_in()
    {
        Assert.Throws<InvalidOperationException>(() => TargetSafety.Validate("https://staging.example.test", false));
    }

    [Fact]
    public void Classifies_localhost_without_confirmation()
    {
        var result = TargetSafety.Validate("http://localhost:5000", false);
        Assert.Equal(TargetKind.Localhost, result.Kind);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task Writes_json_csv_and_html_reports()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atlas-tester-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var report = new TesterReport(DateTimeOffset.UtcNow, [new("user", "/health", 10, 10, 0, 12.5, 18.2)]);
            await TesterReportWriter.WriteJsonAsync(report, Path.Combine(directory, "report.json"));
            await TesterReportWriter.WriteCsvAsync(report, Path.Combine(directory, "report.csv"));
            await TesterReportWriter.WriteHtmlAsync(report, Path.Combine(directory, "report.html"));

            Assert.Contains("/health", await File.ReadAllTextAsync(Path.Combine(directory, "report.json")));
            Assert.Contains("Scenario,Endpoint,Requests", await File.ReadAllTextAsync(Path.Combine(directory, "report.csv")));
            Assert.Contains("Atlas Tester Report", await File.ReadAllTextAsync(Path.Combine(directory, "report.html")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Runs_requested_work_and_aggregates_metrics()
    {
        var runner = new TesterRunner();
        var result = await runner.RunAsync(new(4, 3, TimeSpan.Zero, TimeSpan.FromSeconds(1)), _ => Task.FromResult(new RequestMeasurement("mock", "/health", true, 200, TimeSpan.FromMilliseconds(10))));

        Assert.Equal(12, result.Total);
        Assert.Equal(12, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Equal(12, result.StatusCodes["200"]);
        Assert.Equal(TimeSpan.FromMilliseconds(10), result.P95Latency);
    }

    [Fact]
    public async Task Reports_progress_for_each_measurement()
    {
        var count = 0;
        var progress = new InlineProgress<RequestMeasurement>(_ => count++);
        var runner = new TesterRunner();

        await runner.RunAsync(new(2, 2, TimeSpan.Zero, TimeSpan.FromSeconds(1)), _ => Task.FromResult(new RequestMeasurement("mock", "/health", true, 200, TimeSpan.FromMilliseconds(1))), progress);
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task Supports_cancellation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var runner = new TesterRunner();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(new(10, 100, TimeSpan.Zero, TimeSpan.FromSeconds(1)), async ct =>
        {
            await Task.Delay(100, ct);
            return new RequestMeasurement("mock", "/slow", true, 200, TimeSpan.FromMilliseconds(100));
        }, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Applies_ramp_up_before_each_worker()
    {
        var starts = new System.Collections.Concurrent.ConcurrentBag<DateTimeOffset>();
        var runner = new TesterRunner();
        await runner.RunAsync(new(2, 1, TimeSpan.FromMilliseconds(80), TimeSpan.FromSeconds(1)), _ =>
        {
            starts.Add(DateTimeOffset.UtcNow);
            return Task.FromResult(new RequestMeasurement("mock", "/health", true, 200, TimeSpan.FromMilliseconds(1)));
        });

        Assert.Equal(2, starts.Count);
        Assert.True(starts.Max() - starts.Min() >= TimeSpan.FromMilliseconds(20));
    }

    [Fact]
    public async Task Scenario_store_saves_loads_and_lists_scenarios()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atlas-scenarios-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ScenarioStore(directory);
            var path = await store.SaveAsync(new ScenarioDefinition("market-assets", "Read market assets", [new("step-1", "GET", "/oracle/api/market/assets", "Market assets", true)]));
            var loaded = await store.LoadAsync(path);
            Assert.Equal("market-assets", loaded.Name);
            Assert.Single(loaded.Steps);
            Assert.Single(store.List());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Scenario_store_rejects_empty_workflows()
    {
        var store = new ScenarioStore(Path.Combine(Path.GetTempPath(), "atlas-scenarios-" + Guid.NewGuid().ToString("N")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(new ScenarioDefinition("empty", "", [])));
    }

    [Fact]
    public async Task Scenario_store_uses_the_entered_name_for_separate_files()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atlas-scenarios-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ScenarioStore(directory);
            var first = new ScenarioDefinition("login-flow", "First", [new("1", "POST", "/login", "Login", false)]);
            var second = new ScenarioDefinition("market-assets-flow", "Second", [new("1", "GET", "/assets", "Assets", true)]);
            var firstPath = await store.SaveAsync(first); var secondPath = await store.SaveAsync(second);
            Assert.Equal("login-flow.json", Path.GetFileName(firstPath));
            Assert.Equal("market-assets-flow.json", Path.GetFileName(secondPath));
            Assert.Equal(2, store.List().Count);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Scenario_store_overwrites_only_the_same_entered_name()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atlas-scenarios-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ScenarioStore(directory);
            await store.SaveAsync(new ScenarioDefinition("same-name", "Old", [new("1", "GET", "/old", "Old", false)]));
            await store.SaveAsync(new ScenarioDefinition("same-name", "New", [new("1", "GET", "/new", "New", false)]));
            var saved = store.List(); var loaded = await store.LoadAsync(saved.Single().Path);
            Assert.Single(saved); Assert.Equal("New", loaded.Description); Assert.Equal("/new", loaded.Steps[0].Path);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}

file sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
{
    public void Report(T value) => callback(value);
}
