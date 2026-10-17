using System.Text.Json;

namespace Atlas.Tester.Engine;

public sealed class ProjectCatalogStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string DirectoryPath { get; }

    public ProjectCatalogStore(string? directoryPath = null) =>
        DirectoryPath = directoryPath ?? ClientDataPaths.CatalogsDirectory;

    public async Task SaveAsync(string projectId, OpenApiCatalog catalog, CancellationToken cancellationToken = default)
    {
        ClientDataPaths.EnsureDirectories();
        Directory.CreateDirectory(DirectoryPath);
        var snapshot = new CatalogSnapshot(DateTimeOffset.UtcNow, catalog.Operations);
        await File.WriteAllTextAsync(Path.Combine(DirectoryPath, projectId + ".json"), JsonSerializer.Serialize(snapshot, Options), cancellationToken);
    }

    public async Task<OpenApiCatalog?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(DirectoryPath, projectId + ".json");
        if (!File.Exists(path)) return null;
        var snapshot = JsonSerializer.Deserialize<CatalogSnapshot>(await File.ReadAllTextAsync(path, cancellationToken), Options);
        return snapshot is null || snapshot.Operations.Count == 0 ? null : OpenApiCatalog.FromOperations(snapshot.Operations);
    }

    public Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(DirectoryPath, projectId + ".json");
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private sealed record CatalogSnapshot(DateTimeOffset CachedAtUtc, IReadOnlyList<OpenApiOperation> Operations);
}
