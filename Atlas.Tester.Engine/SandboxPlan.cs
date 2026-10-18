namespace Atlas.Tester.Engine;

public sealed record SandboxDependency(string Name, string Purpose);

public sealed record SandboxRunPlan(
    string RunId,
    string NetworkName,
    string BackendProjectPath,
    string StartCommand,
    Uri Target,
    IReadOnlyList<SandboxDependency> Dependencies,
    IReadOnlyDictionary<string, string> EnvironmentOverrides);

public static class SandboxPlanner
{
    private static readonly SandboxDependency[] DefaultDependencies =
    [
        new("PostgreSQL", "Disposable database; no host database port is exposed."),
        new("SMS mock", "Receives provider-compatible SMS requests inside the private network."),
        new("External HTTP mocks", "Only explicitly configured external services are reachable.")
    ];

    public static SandboxRunPlan Create(ProjectDefinition project)
    {
        if (string.IsNullOrWhiteSpace(project.SandboxBackendProjectPath))
            throw new InvalidOperationException("Choose the backend project file or folder for the isolated sandbox.");
        if (!Path.Exists(project.SandboxBackendProjectPath))
            throw new InvalidOperationException($"Sandbox backend path does not exist: {project.SandboxBackendProjectPath}");
        if (!Uri.TryCreate(project.BackendUrl, UriKind.Absolute, out var target))
            throw new InvalidOperationException("The project backend URL must be an absolute URL.");

        var runId = Guid.NewGuid().ToString("N");
        var startCommand = string.IsNullOrWhiteSpace(project.SandboxStartCommand)
            ? $"dotnet run --project \"{project.SandboxBackendProjectPath}\""
            : project.SandboxStartCommand.Trim();
        return new SandboxRunPlan(
            runId,
            $"atlas-tester-{runId[..12]}",
            project.SandboxBackendProjectPath,
            startCommand,
            target,
            DefaultDependencies,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ASPNETCORE_URLS"] = target.ToString().TrimEnd('/'),
                ["ATLAS_TESTER_SANDBOX"] = "true"
            });
    }
}
