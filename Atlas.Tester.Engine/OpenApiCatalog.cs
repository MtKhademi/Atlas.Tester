using System.Text.Json;

namespace Atlas.Tester.Engine;

public sealed class OpenApiCatalog
{
    public IReadOnlyList<OpenApiOperation> Operations { get; }
    private readonly HashSet<string> operations;

    private OpenApiCatalog(HashSet<string> operations, IReadOnlyList<OpenApiOperation> definitions) { this.operations = operations; Operations = definitions; }

    public static async Task<OpenApiCatalog> LoadAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        TesterLogging.Logger.Information("Loading OpenAPI document from {BaseAddress}openapi/v1.json", client.BaseAddress);
        using var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        var operations = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var definitions = new List<OpenApiOperation>();
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                operations.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
                var tag = operation.Value.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array && tags.GetArrayLength() > 0 ? tags[0].GetString() ?? "Other" : "Other";
                definitions.Add(new(operation.Name.ToUpperInvariant(), path.Name, $"{operation.Name.ToUpperInvariant()} {path.Name}", tag, false));
            }
        }
        TesterLogging.Logger.Information("OpenAPI loaded: {OperationCount} operations", operations.Count);
        return new(operations, definitions);
    }

    public void Validate(TesterScenario scenario)
    {
        var missing = scenario.Steps
            .Where(step => !operations.Contains($"{step.Method.ToUpperInvariant()} {step.Path.Split('?', 2)[0]}"))
            .Select(step => $"{step.Method.ToUpperInvariant()} {step.Path}")
            .ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Scenario '{scenario.Key}' is not compatible with the backend OpenAPI document. Missing: {string.Join(", ", missing)}");
    }
}

public sealed record OpenApiOperation(string Method, string Path, string DisplayName, string TagName, bool RequiresAuthentication);
