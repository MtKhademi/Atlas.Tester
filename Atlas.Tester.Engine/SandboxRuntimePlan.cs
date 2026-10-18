namespace Atlas.Tester.Engine;

public sealed record SandboxDatabaseSpec(string ContainerName, string VolumeName, string HostName, string DatabaseName, string UserName, string Password);
public sealed record SandboxBackendSpec(string ContainerName, int HostPort, int ContainerPort, string Image, string WorkingDirectory, IReadOnlyDictionary<string, string> Environment);
public sealed record SandboxRuntimePlan(SandboxRunPlan Run, SandboxDatabaseSpec Database, SandboxBackendSpec Backend)
{
    public const string ManagedLabel = "atlas-tester.managed=true";

    public static SandboxRuntimePlan Create(SandboxRunPlan run, int hostPort, string backendImage, string workingDirectory, string connectionStringEnvironmentVariable)
    {
        if (hostPort is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(hostPort));
        if (string.IsNullOrWhiteSpace(backendImage)) throw new ArgumentException("A backend image is required.", nameof(backendImage));
        if (string.IsNullOrWhiteSpace(connectionStringEnvironmentVariable)) throw new ArgumentException("A backend connection-string environment variable is required.", nameof(connectionStringEnvironmentVariable));
        var suffix = run.RunId[..12];
        var database = new SandboxDatabaseSpec($"atlas-tester-db-{suffix}", $"atlas-tester-db-data-{suffix}", "postgres", "atlas_tester", "atlas_tester", Guid.NewGuid().ToString("N"));
        var connectionString = $"Host={database.HostName};Port=5432;Database={database.DatabaseName};Username={database.UserName};Password={database.Password}";
        var environment = new Dictionary<string, string>(run.EnvironmentOverrides, StringComparer.OrdinalIgnoreCase)
        {
            [connectionStringEnvironmentVariable] = connectionString,
            ["ASPNETCORE_URLS"] = $"http://+:{8080}"
        };
        return new SandboxRuntimePlan(run, database, new SandboxBackendSpec($"atlas-tester-api-{suffix}", hostPort, 8080, backendImage, workingDirectory, environment));
    }

    public IReadOnlyList<IReadOnlyList<string>> DockerCommands()
    {
        var labels = new[] { "--label", ManagedLabel, "--label", $"atlas-tester.run-id={Run.RunId}" };
        var network = new List<string> { "network", "create", "--internal" }; network.AddRange(labels); network.Add(Run.NetworkName);
        var volume = new List<string> { "volume", "create" }; volume.AddRange(labels); volume.Add(Database.VolumeName);
        var database = new List<string> { "run", "-d", "--name", Database.ContainerName, "--network", Run.NetworkName, "--network-alias", Database.HostName }; database.AddRange(labels); database.AddRange(["-e", $"POSTGRES_DB={Database.DatabaseName}", "-e", $"POSTGRES_USER={Database.UserName}", "-e", $"POSTGRES_PASSWORD={Database.Password}", "-v", $"{Database.VolumeName}:/var/lib/postgresql/data", "postgres:16-alpine"]);
        var backend = new List<string> { "run", "-d", "--name", Backend.ContainerName, "--network", Run.NetworkName, "-p", $"127.0.0.1:{Backend.HostPort}:{Backend.ContainerPort}" }; backend.AddRange(labels); foreach (var pair in Backend.Environment) backend.AddRange(["-e", $"{pair.Key}={pair.Value}"]); backend.Add(Backend.Image);
        var commands = new List<IReadOnlyList<string>> { network, volume, database, backend };
        return commands;
    }
}
