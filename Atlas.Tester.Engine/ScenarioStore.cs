using System.Text.Json;

namespace Atlas.Tester.Engine;

public sealed record ScenarioDefinition(string Name, string Description, IReadOnlyList<ScenarioNodeDefinition> Steps)
{
    public const int CurrentVersion = 2;
    public int Version { get; init; } = CurrentVersion;
    public IReadOnlyList<ScenarioVariableDefinition> Variables { get; init; } = [];
}

public sealed record ScenarioVariableDefinition(string Name, string Value, bool IsSecret = false);

public sealed record ScenarioNodeDefinition(string Id, string Method, string Path, string DisplayName, bool RequiresAuthentication)
{
    public ScenarioRequestDefinition Request { get; init; } = new(Method, Path);
    public IReadOnlyList<ScenarioCaptureDefinition> Captures { get; init; } = [];
    public IReadOnlyList<ScenarioAssertionDefinition> Assertions { get; init; } = [];
}

public sealed record ScenarioRequestDefinition(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyDictionary<string, string>? Query = null,
    IReadOnlyDictionary<string, string>? PathParameters = null,
    string? BodyJson = null);

public sealed record ScenarioCaptureDefinition(string VariableName, string JsonPath);

public sealed record ScenarioAssertionDefinition(string Type, string? ExpectedValue = null, string? JsonPath = null);
public sealed record SavedScenario(string Name, string Path, DateTimeOffset LastModifiedUtc, long SizeBytes);

public sealed class ScenarioStore(string? directory = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string DirectoryPath { get; } = directory ?? ClientDataPaths.ScenariosDirectory;

    public async Task<string> SaveAsync(ScenarioDefinition scenario, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scenario.Name)) throw new ArgumentException("Scenario name is required.", nameof(scenario));
        if (scenario.Steps.Count == 0) throw new InvalidOperationException("A scenario must contain at least one step.");
        System.IO.Directory.CreateDirectory(DirectoryPath);
        var safeName = string.Join("-", scenario.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName)) throw new ArgumentException("Scenario name does not contain a valid file name.", nameof(scenario));
        var path = Path.Combine(DirectoryPath, $"{safeName}.json");
        var versionedScenario = scenario with { Version = ScenarioDefinition.CurrentVersion };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(versionedScenario, JsonOptions), cancellationToken);
        TesterLogging.Logger.Information("Scenario stored by engine: {Path}", path);
        return path;
    }

    public async Task<ScenarioDefinition> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        using var document = JsonDocument.Parse(json);
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, JsonOptions) ?? throw new InvalidOperationException("Saved scenario is empty or invalid.");
        return document.RootElement.TryGetProperty(nameof(ScenarioDefinition.Version), out _)
            ? scenario
            : scenario with { Version = 1 };
    }

    public IReadOnlyList<SavedScenario> List() => System.IO.Directory.Exists(DirectoryPath)
        ? System.IO.Directory.EnumerateFiles(DirectoryPath, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Select(path => new SavedScenario(Path.GetFileNameWithoutExtension(path), path, File.GetLastWriteTimeUtc(path), new FileInfo(path).Length)).ToArray()
        : [];
}
