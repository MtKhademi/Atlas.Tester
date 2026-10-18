using System.Diagnostics;

namespace Atlas.Tester.Engine;

public sealed class SandboxRuntime : IAsyncDisposable
{
    private static readonly HashSet<SandboxRuntime> ActiveRuntimes = [];
    private static readonly object ActiveRuntimesLock = new();
    private readonly SandboxRuntimePlan plan;
    private readonly List<string> created = [];
    private readonly List<string> output = [];
    private Process? backend;
    public SmsMockServer? SmsMock { get; private set; }

    public SandboxRuntime(SandboxRuntimePlan plan)
    {
        this.plan = plan;
        lock (ActiveRuntimesLock) ActiveRuntimes.Add(this);
    }

    public static async Task DisposeAllAsync()
    {
        SandboxRuntime[] runtimes;
        lock (ActiveRuntimesLock) runtimes = ActiveRuntimes.ToArray();
        foreach (var runtime in runtimes) await runtime.DisposeAsync();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SmsMock = new SmsMockServer();
            await SmsMock.StartAsync(cancellationToken);
            await DockerAsync(["network", "create", "--label", SandboxRuntimePlan.ManagedLabel, "--label", $"atlas-tester.run-id={plan.Run.RunId}", plan.Run.NetworkName], cancellationToken); created.Add($"network:{plan.Run.NetworkName}");
            await DockerAsync(["volume", "create", "--label", SandboxRuntimePlan.ManagedLabel, "--label", $"atlas-tester.run-id={plan.Run.RunId}", plan.Database.VolumeName], cancellationToken); created.Add($"volume:{plan.Database.VolumeName}");
            await DockerAsync(["run", "-d", "--name", plan.Database.ContainerName, "--network", plan.Run.NetworkName, "--network-alias", plan.Database.HostName, "-p", $"127.0.0.1:{plan.Database.HostPort}:5432", "--label", SandboxRuntimePlan.ManagedLabel, "--label", $"atlas-tester.run-id={plan.Run.RunId}", "-e", $"POSTGRES_DB={plan.Database.DatabaseName}", "-e", $"POSTGRES_USER={plan.Database.UserName}", "-e", $"POSTGRES_PASSWORD={plan.Database.Password}", "-v", $"{plan.Database.VolumeName}:/var/lib/postgresql/data", "postgres:16-alpine"], cancellationToken); created.Add($"container:{plan.Database.ContainerName}");
            await WaitForDatabaseAsync(cancellationToken);
        }
        catch { await DisposeAsync(); throw; }
    }

    public async Task StartBackendAsync(string command, IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken = default)
    {
        if (backend is not null) throw new InvalidOperationException("The sandbox backend is already running.");
        var info = new ProcessStartInfo("cmd.exe", $"/d /s /c \"{command}\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(plan.Run.BackendProjectPath) ?? Environment.CurrentDirectory };
        foreach (var pair in environment) info.Environment[pair.Key] = pair.Value;
        info.Environment["ATLAS_TESTER_SANDBOX"] = "true";
        var connectionString = $"Host=127.0.0.1;Port={plan.Database.HostPort};Database={plan.Database.DatabaseName};Username={plan.Database.UserName};Password={plan.Database.Password}";
        info.Environment["IAM__ConnectionString"] = connectionString;
        info.Environment["Oracle__ConnectionString"] = connectionString;
        if (SmsMock is not null)
        {
            info.Environment["IAM__PasswordReset__SmsIr__Enabled"] = "true";
            info.Environment["IAM__PasswordReset__SmsIr__BaseUrl"] = SmsMock.BaseAddress.ToString();
            info.Environment["IAM__PasswordReset__SmsIr__ApiKey"] = "atlas-tester-mock";
            info.Environment["IAM__PasswordReset__SmsIr__TemplateId"] = "123456";
        }
        backend = Process.Start(info) ?? throw new InvalidOperationException("Could not start the sandbox backend process.");
        backend.OutputDataReceived += (_, line) => { if (!string.IsNullOrWhiteSpace(line.Data)) output.Add(line.Data); };
        backend.ErrorDataReceived += (_, line) => { if (!string.IsNullOrWhiteSpace(line.Data)) output.Add(line.Data); };
        backend.BeginOutputReadLine(); backend.BeginErrorReadLine();
        await Task.Delay(1, cancellationToken);
    }

    public async Task WaitForReadyAsync(string openApiPath, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTimeOffset.UtcNow + timeout;
        var endpoint = new Uri(plan.Run.Target, openApiPath);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.GetAsync(endpoint, cancellationToken);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            if (backend?.HasExited == true) throw new InvalidOperationException($"Sandbox backend exited before becoming ready.{Environment.NewLine}{string.Join(Environment.NewLine, output.TakeLast(30))}");
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
        throw new TimeoutException($"Sandbox backend did not expose {openApiPath} within {timeout.TotalSeconds:0} seconds.{Environment.NewLine}{string.Join(Environment.NewLine, output.TakeLast(30))}");
    }

    private async Task WaitForDatabaseAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try { await DockerAsync(["exec", plan.Database.ContainerName, "pg_isready", "-U", plan.Database.UserName, "-d", plan.Database.DatabaseName], cancellationToken); return; }
            catch (InvalidOperationException) { await Task.Delay(500, cancellationToken); }
        }
        throw new TimeoutException("Sandbox PostgreSQL did not become ready within 15 seconds.");
    }

    private static async Task DockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("docker") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Docker could not be started.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken); var error = await process.StandardError.ReadToEndAsync(cancellationToken); await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException($"Docker command failed: {error.Trim()} {output.Trim()}".Trim());
    }

    public async ValueTask DisposeAsync()
    {
        if (backend is { HasExited: false }) backend.Kill(true);
        foreach (var item in created.AsEnumerable().Reverse())
        {
            var pair = item.Split(':', 2); var command = pair[0] switch { "container" => new[] { "rm", "-f", pair[1] }, "volume" => new[] { "volume", "rm", "-f", pair[1] }, "network" => new[] { "network", "rm", pair[1] }, _ => [] };
            try { await DockerAsync(command, CancellationToken.None); } catch { }
        }
        created.Clear(); backend?.Dispose(); backend = null;
        if (SmsMock is not null)
        {
            try { await SmsMock.DisposeAsync(); }
            catch (Exception ex) { TesterLogging.Logger.Warning(ex, "Sandbox SMS mock cleanup failed"); }
        }
        SmsMock = null;
        lock (ActiveRuntimesLock) ActiveRuntimes.Remove(this);
    }
}
