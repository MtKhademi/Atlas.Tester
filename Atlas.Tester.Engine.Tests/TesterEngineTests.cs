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
            var report = new TesterReport(DateTimeOffset.UtcNow, [new("user", "/health", 10, 10, 0, 12.5, 18.2)], [new("user", "/health", "GET", "http://localhost:5000/health", null, 200, "{\"status\":\"ok\"}", null, 12.5, true)]);
            await TesterReportWriter.WriteJsonAsync(report, Path.Combine(directory, "report.json"));
            await TesterReportWriter.WriteCsvAsync(report, Path.Combine(directory, "report.csv"));
            await TesterReportWriter.WriteRequestCsvAsync(report, Path.Combine(directory, "requests.csv"));
            await TesterReportWriter.WriteHtmlAsync(report, Path.Combine(directory, "report.html"));

            Assert.Contains("/health", await File.ReadAllTextAsync(Path.Combine(directory, "report.json")));
            Assert.Contains("Scenario,Endpoint,Requests", await File.ReadAllTextAsync(Path.Combine(directory, "report.csv")));
            Assert.Contains("ResponseBody", await File.ReadAllTextAsync(Path.Combine(directory, "requests.csv")));
            Assert.Contains("Atlas Tester Report", await File.ReadAllTextAsync(Path.Combine(directory, "report.html")));
            Assert.Contains("status", await File.ReadAllTextAsync(Path.Combine(directory, "report.html")));
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
    public async Task Scenario_runner_preserves_the_order_of_nodes_for_a_single_worker()
    {
        var runner = new TesterRunner();
        var results = await runner.RunScenarioAsync(new(1, 1, TimeSpan.Zero, TimeSpan.FromSeconds(1)), _ => Task.FromResult<IReadOnlyList<RequestMeasurement>>([
            new("auth", "/register", true, 200, TimeSpan.FromMilliseconds(1)),
            new("auth", "/login", true, 200, TimeSpan.FromMilliseconds(1))]));

        Assert.Equal(["/register", "/login"], results.Select(x => x.Endpoint));
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
            Assert.Equal(ScenarioDefinition.CurrentVersion, loaded.Version);
            Assert.Single(loaded.Steps);
            Assert.Equal("GET", loaded.Steps[0].Request.Method);
            Assert.Equal("/oracle/api/market/assets", loaded.Steps[0].Request.Path);
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

    [Fact]
    public async Task Scenario_store_loads_legacy_json_as_version_one()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atlas-scenarios-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "legacy.json");
            await File.WriteAllTextAsync(path, """
                {
                  "Name": "legacy-login",
                  "Description": "Legacy format",
                  "Steps": [
                    {
                      "Id": "login",
                      "Method": "POST",
                      "Path": "/auth/login",
                      "DisplayName": "Login",
                      "RequiresAuthentication": false
                    }
                  ]
                }
                """);

            var scenario = await new ScenarioStore(directory).LoadAsync(path);

            Assert.Equal(1, scenario.Version);
            Assert.Equal("POST", scenario.Steps[0].Request.Method);
            Assert.Equal("/auth/login", scenario.Steps[0].Request.Path);
            Assert.Empty(scenario.Steps[0].Captures);
            Assert.Empty(scenario.Steps[0].Assertions);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Scenario_store_persists_node_request_captures_and_assertions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atlas-scenarios-" + Guid.NewGuid().ToString("N"));
        try
        {
            var node = new ScenarioNodeDefinition("login", "POST", "/auth/login", "Login", false)
            {
                Request = new ScenarioRequestDefinition("POST", "/auth/login", new Dictionary<string, string> { ["X-Tenant"] = "atlas" }, BodyJson: "{\"email\":\"{{adminEmail}}\"}"),
                Captures = [new ScenarioCaptureDefinition("accessToken", "$.accessToken")],
                Assertions = [new ScenarioAssertionDefinition("statusCode", "200"), new ScenarioAssertionDefinition("jsonPathExists", JsonPath: "$.accessToken")]
            };
            var store = new ScenarioStore(directory);
            var path = await store.SaveAsync(new ScenarioDefinition("super-admin-login", "Logs in", [node]));
            var loaded = await store.LoadAsync(path);

            Assert.Equal(ScenarioDefinition.CurrentVersion, loaded.Version);
            Assert.Equal("{\"email\":\"{{adminEmail}}\"}", loaded.Steps[0].Request.BodyJson);
            Assert.Equal("atlas", loaded.Steps[0].Request.Headers!["X-Tenant"]);
            Assert.Equal(new ScenarioCaptureDefinition("accessToken", "$.accessToken"), Assert.Single(loaded.Steps[0].Captures));
            Assert.Equal("statusCode", loaded.Steps[0].Assertions[0].Type);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Scenario_executor_applies_request_templates_and_variables_from_a_prior_step()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath == "/login"
            ? new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{\"accessToken\":\"token-123\"}") }
            : new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://atlas.test") };
        var login = new ScenarioStep("Login", "POST", "/login", false, new ScenarioRequestDefinition("POST", "/login", new Dictionary<string, string> { ["X-User"] = "{{emailOrPhone}}" }, BodyJson: "{\"password\":\"{{password}}\"}"));
        var read = new ScenarioStep("Read profile", "GET", "/profiles/{token}", true, new ScenarioRequestDefinition("GET", "/profiles/{token}", new Dictionary<string, string> { ["X-Token"] = "{{accessToken}}" }, new Dictionary<string, string> { ["source"] = "{{accessToken}}" }, new Dictionary<string, string> { ["token"] = "{{accessToken}}" }));

        var results = await new HttpScenarioExecutor(client, new TesterScenario("template", "Template", [login, read]), new ScenarioCredentials("admin@atlas.test", "secret")).RunAsync();

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal("{\"password\":\"secret\"}", handler.Requests[0].Body);
        Assert.Equal("admin@atlas.test", handler.Requests[0].Headers["X-User"]);
        Assert.Equal("/profiles/token-123?source=token-123", handler.Requests[1].PathAndQuery);
        Assert.Equal("token-123", handler.Requests[1].Headers["X-Token"]);
        Assert.Equal("Bearer token-123", handler.Requests[1].Headers["Authorization"]);
    }

    [Fact]
    public async Task Scenario_executor_reports_an_unresolved_variable_without_sending_a_request()
    {
        var handler = new RecordingHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://atlas.test") };
        var step = new ScenarioStep("Get profile", "GET", "/profiles", false, new ScenarioRequestDefinition("GET", "/profiles", Query: new Dictionary<string, string> { ["id"] = "{{missingId}}" }));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpScenarioExecutor(client, new TesterScenario("missing", "Missing", [step]), new ScenarioCredentials("email", "password")).RunAsync());

        Assert.Contains("missingId", error.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Scenario_executor_captures_a_json_value_for_a_later_node()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath == "/register"
            ? new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Created) { Content = new System.Net.Http.StringContent("{\"user\":{\"email\":\"new-user@atlas.test\"}}") }
            : new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://atlas.test") };
        var register = new ScenarioStep("Register", "POST", "/register", false, new ScenarioRequestDefinition("POST", "/register", BodyJson: "{}"), [new ScenarioCaptureDefinition("registeredEmail", "$.user.email")]);
        var login = new ScenarioStep("Login", "POST", "/login", false, new ScenarioRequestDefinition("POST", "/login", BodyJson: "{\"email\":\"{{registeredEmail}}\"}"));

        var results = await new HttpScenarioExecutor(client, new TesterScenario("register-login", "Register then login", [register, login]), new ScenarioCredentials("unused", "password")).RunAsync();

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal("{\"email\":\"new-user@atlas.test\"}", handler.Requests[1].Body);
    }

    [Fact]
    public async Task Scenario_executor_reports_node_variable_and_selector_when_capture_fails()
    {
        var handler = new RecordingHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{}") });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://atlas.test") };
        var step = new ScenarioStep("Register", "POST", "/register", false, new ScenarioRequestDefinition("POST", "/register", BodyJson: "{}"), [new ScenarioCaptureDefinition("registeredEmail", "$.user.email")]);

        var result = Assert.Single(await new HttpScenarioExecutor(client, new TesterScenario("capture-fail", "Capture fail", [step]), new ScenarioCredentials("unused", "password")).RunAsync());

        Assert.False(result.Succeeded);
        Assert.Contains("Register", result.Error);
        Assert.Contains("registeredEmail", result.Error);
        Assert.Contains("$.user.email", result.Error);
    }

    [Fact]
    public async Task Scenario_executor_accepts_an_expected_unauthorized_status_code()
    {
        var handler = new RecordingHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://atlas.test") };
        var step = new ScenarioStep("Denied login", "POST", "/login", false, new ScenarioRequestDefinition("POST", "/login", BodyJson: "{}"), Assertions: [new ScenarioAssertionDefinition("statusCode", "401")]);

        var result = Assert.Single(await new HttpScenarioExecutor(client, new TesterScenario("expected-401", "Expected 401", [step]), new ScenarioCredentials("email", "password")).RunAsync());

        Assert.True(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task Scenario_executor_reports_an_unexpected_status_code()
    {
        var handler = new RecordingHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://atlas.test") };
        var step = new ScenarioStep("Find profile", "GET", "/profile", false, Assertions: [new ScenarioAssertionDefinition("statusCode", "200")]);

        var result = Assert.Single(await new HttpScenarioExecutor(client, new TesterScenario("unexpected-404", "Unexpected 404", [step]), new ScenarioCredentials("email", "password")).RunAsync());

        Assert.False(result.Succeeded);
        Assert.Contains("expected status code 200", result.Error);
        Assert.Contains("404", result.Error);
    }

    [Fact]
    public async Task Scenario_executor_checks_json_path_assertions()
    {
        var handler = new RecordingHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{\"user\":{\"role\":\"admin\"}}") });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://atlas.test") };
        var step = new ScenarioStep("Read user", "GET", "/me", false, Assertions: [new ScenarioAssertionDefinition("jsonPathExists", JsonPath: "$.user.role"), new ScenarioAssertionDefinition("jsonPathEquals", "admin", "$.user.role")]);

        var result = Assert.Single(await new HttpScenarioExecutor(client, new TesterScenario("json-assertion", "JSON assertion", [step]), new ScenarioCredentials("email", "password")).RunAsync());

        Assert.True(result.Succeeded);
    }
}

file sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
{
    public void Report(T value) => callback(value);
}

file sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => SendAsync(request, cancellationToken).GetAwaiter().GetResult();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase);
        if (request.Content is not null)
            foreach (var header in request.Content.Headers) headers[header.Key] = string.Join(",", header.Value);
        Requests.Add(new RecordedRequest(request.RequestUri!.PathAndQuery, headers, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
        return responseFactory(request);
    }
}

file sealed record RecordedRequest(string PathAndQuery, IReadOnlyDictionary<string, string> Headers, string? Body);
