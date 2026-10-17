using System.Text.Json;

namespace Atlas.Tester.Engine;

public sealed class OpenApiCatalog
{
    public IReadOnlyList<OpenApiOperation> Operations { get; }
    private readonly HashSet<string> operations;

    private OpenApiCatalog(HashSet<string> operations, IReadOnlyList<OpenApiOperation> definitions) { this.operations = operations; Operations = definitions; }

    public static async Task<OpenApiCatalog> LoadAsync(HttpClient client, string openApiPath = "/openapi/v1.json", CancellationToken cancellationToken = default)
    {
        if (client.BaseAddress is null) throw new InvalidOperationException("An API base address is required to load an OpenAPI catalog.");
        var path = string.IsNullOrWhiteSpace(openApiPath) ? "/openapi/v1.json" : "/" + openApiPath.TrimStart('/');
        var documentUri = new Uri(client.BaseAddress, path);
        TesterLogging.Logger.Information("Loading OpenAPI document from {DocumentUri}", documentUri);
        using var response = await client.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"OpenAPI document was not found at {documentUri} (HTTP {(int)response.StatusCode} {response.ReasonPhrase}). Update the project's OpenAPI path and try again.", null, response.StatusCode);
        }
        return FromJson(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public static OpenApiCatalog FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("The OpenAPI document does not contain a valid 'paths' object.");
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

    public static OpenApiCatalog FromOperations(IReadOnlyList<OpenApiOperation> definitions) =>
        new(definitions.Select(operation => $"{operation.Method.ToUpperInvariant()} {operation.Path}").ToHashSet(StringComparer.OrdinalIgnoreCase), definitions);

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
