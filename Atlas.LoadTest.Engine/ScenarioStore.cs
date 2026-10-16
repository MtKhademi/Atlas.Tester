using System.Text.Json;

namespace Atlas.LoadTest.Engine;

public sealed record ScenarioDefinition(string Name, string Description, IReadOnlyList<ScenarioNodeDefinition> Steps);
public sealed record ScenarioNodeDefinition(string Id, string Method, string Path, string DisplayName, bool RequiresAuthentication);
public sealed record SavedScenario(string Name, string Path, DateTimeOffset LastModifiedUtc, long SizeBytes);

public sealed class ScenarioStore(string directory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string DirectoryPath { get; } = directory;

    public async Task<string> SaveAsync(ScenarioDefinition scenario, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scenario.Name)) throw new ArgumentException("Scenario name is required.", nameof(scenario));
        if (scenario.Steps.Count == 0) throw new InvalidOperationException("A scenario must contain at least one step.");
        System.IO.Directory.CreateDirectory(DirectoryPath);
        var safeName = string.Join("-", scenario.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName)) throw new ArgumentException("Scenario name does not contain a valid file name.", nameof(scenario));
        var path = Path.Combine(DirectoryPath, $"{safeName}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(scenario, JsonOptions), cancellationToken);
        LoadTestLogging.Logger.Information("Scenario stored by engine: {Path}", path);
        return path;
    }

    public async Task<ScenarioDefinition> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(await File.ReadAllTextAsync(path, cancellationToken), JsonOptions) ?? throw new InvalidOperationException("Saved scenario is empty or invalid.");
        return scenario;
    }

    public IReadOnlyList<SavedScenario> List() => System.IO.Directory.Exists(DirectoryPath)
        ? System.IO.Directory.EnumerateFiles(DirectoryPath, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Select(path => new SavedScenario(Path.GetFileNameWithoutExtension(path), path, File.GetLastWriteTimeUtc(path), new FileInfo(path).Length)).ToArray()
        : [];
}
