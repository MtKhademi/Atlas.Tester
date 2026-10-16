using System.Text.Json;

namespace Atlas.Tester.Engine;

public sealed record ProjectDefinition(string Id, string Name, string Description, string BackendUrl, string OpenApiPath, DateTimeOffset UpdatedAtUtc);

public sealed class ProjectStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string DirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasTester", "projects");
    public IReadOnlyList<ProjectDefinition> List() => Directory.Exists(DirectoryPath) ? Directory.EnumerateFiles(DirectoryPath, "*.json").Select(x => JsonSerializer.Deserialize<ProjectDefinition>(File.ReadAllText(x), Options)).Where(x => x is not null).Cast<ProjectDefinition>().OrderBy(x => x.Name).ToArray() : [];
    public async Task SaveAsync(ProjectDefinition project, CancellationToken ct = default) { Directory.CreateDirectory(DirectoryPath); await File.WriteAllTextAsync(Path.Combine(DirectoryPath, project.Id + ".json"), JsonSerializer.Serialize(project, Options), ct); }
    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var path = Path.Combine(DirectoryPath, id + ".json");
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
