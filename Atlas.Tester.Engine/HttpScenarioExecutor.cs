using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Tester.Engine;

public sealed record ScenarioCredentials(string EmailOrPhone, string Password);

public sealed partial class HttpScenarioExecutor(HttpClient client, TesterScenario scenario, ScenarioCredentials credentials)
{
    private readonly Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["emailOrPhone"] = credentials.EmailOrPhone,
        ["password"] = credentials.Password
    };

    public async Task<IReadOnlyList<RequestMeasurement>> RunAsync(CancellationToken cancellationToken = default)
    {
        TesterLogging.Logger.Information("Scenario started: {Scenario}", scenario.Key);
        var results = new List<RequestMeasurement>();
        foreach (var step in scenario.Steps)
        {
            var path = BuildPath(step);
            using var request = new HttpRequestMessage(new HttpMethod(step.Method), path);
            string? requestBody = null;
            if (step.RequiresAuthentication && variables.TryGetValue("accessToken", out var accessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            ApplyHeaders(request, step.Request?.Headers);
            if (!string.IsNullOrWhiteSpace(step.Request?.BodyJson))
            {
                requestBody = ResolveVariables(step.Request.BodyJson, $"body for '{step.Name}'");
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            }
            else if (step.Name.Contains("login", StringComparison.OrdinalIgnoreCase))
            {
                requestBody = JsonSerializer.Serialize(credentials);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            }
            else if (step.Name.Contains("user asset", StringComparison.OrdinalIgnoreCase))
                request.Content = JsonContent.Create(new { marketAssetId = "0RWVV8YYAVVYW", quantity = 1.0m, averageUnitCost = 1000m, note = "tester" });
            else if (step.Name.Contains("transaction", StringComparison.OrdinalIgnoreCase))
                request.Content = JsonContent.Create(new { quantity = 1.0m, unitPrice = 1000m, occurredAtUtc = DateTimeOffset.UtcNow });
            else if (step.Name.Contains("bank account", StringComparison.OrdinalIgnoreCase))
                request.Content = JsonContent.Create(new { bankCode = "MELLAT", accountNumber = "1234567890", iban = "IR820540102680020817909002", cardNumber = "6104337987654322", title = "Tester", isDefault = false });
            else if (step.Method is "POST" or "PUT" or "PATCH")
                request.Content = JsonContent.Create(new { price = 1234567.89m, symbol = "gold18" });

            var started = Stopwatch.GetTimestamp();
            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                var duration = Stopwatch.GetElapsedTime(started);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                string? captureError = null;
                if (step.Captures?.Count > 0)
                {
                    try { CaptureResponse(step, responseBody); }
                    catch (InvalidOperationException ex) { captureError = ex.Message; }
                }
                if (response.IsSuccessStatusCode)
                {
                    if (step.Name.Contains("login", StringComparison.OrdinalIgnoreCase)) SetVariableFromResponse("accessToken", responseBody);
                    if (!variables.ContainsKey("id")) SetVariableFromResponse("id", responseBody);
                }
                var error = captureError ?? (response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})");
                results.Add(new(scenario.Key, step.Path, response.IsSuccessStatusCode && captureError is null, (int)response.StatusCode, duration, error, step.Method, response.RequestMessage?.RequestUri?.ToString() ?? path, requestBody, responseBody));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                TesterLogging.Logger.Error(ex, "HTTP request failed: {Method} {Path}", step.Method, step.Path);
                results.Add(new(scenario.Key, step.Path, false, null, Stopwatch.GetElapsedTime(started), ex.Message, step.Method, path, requestBody));
            }
        }
        return results;
    }

    private string BuildPath(ScenarioStep step)
    {
        var request = step.Request;
        var path = request is null ? step.Path : request.Path;
        var parameters = request?.PathParameters ?? new Dictionary<string, string>();
        foreach (var parameter in parameters)
            path = path.Replace($"{{{parameter.Key}}}", ResolveVariables(parameter.Value, $"path parameter '{parameter.Key}' for '{step.Name}'"), StringComparison.Ordinal);
        if (path.Contains("{id}", StringComparison.Ordinal) && variables.TryGetValue("id", out var id))
            path = path.Replace("{id}", id, StringComparison.Ordinal);
        if (Regex.IsMatch(path, "\\{[^}]+\\}"))
            throw new InvalidOperationException($"Unresolved path parameter in request for '{step.Name}'.");
        if (request?.Query is null || request.Query.Count == 0) return path;
        var separator = path.Contains('?') ? '&' : '?';
        var query = string.Join("&", request.Query.Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(ResolveVariables(item.Value, $"query parameter '{item.Key}' for '{step.Name}'"))}"));
        return $"{path}{separator}{query}";
    }

    private void ApplyHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null) return;
        foreach (var header in headers)
        {
            var value = ResolveVariables(header.Value, $"header '{header.Key}'");
            if (request.Headers.TryAddWithoutValidation(header.Key, value)) continue;
            request.Content ??= new StringContent(string.Empty);
            if (!request.Content.Headers.TryAddWithoutValidation(header.Key, value))
                throw new InvalidOperationException($"Header '{header.Key}' is not valid for a scenario request.");
        }
    }

    private string ResolveVariables(string template, string location) => VariablePattern().Replace(template, match =>
    {
        var variableName = match.Groups[1].Value.Trim();
        if (variables.TryGetValue(variableName, out var value)) return value;
        throw new InvalidOperationException($"Scenario variable '{variableName}' required by {location} has not been set.");
    });

    private void SetVariable(string variableName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) variables[variableName] = value;
    }

    private void CaptureResponse(ScenarioStep step, string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            throw new InvalidOperationException($"Node '{step.Name}' could not capture response values because the response body is empty.");
        JsonDocument document;
        try { document = JsonDocument.Parse(responseBody); }
        catch (JsonException) { throw new InvalidOperationException($"Node '{step.Name}' could not capture response values because the response is not valid JSON."); }
        using (document)
        {
            foreach (var capture in step.Captures!)
            {
                try { SetVariable(capture.VariableName, JsonPathValueResolver.Resolve(document.RootElement, capture.JsonPath)); }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException($"Node '{step.Name}' could not capture '{capture.VariableName}' from '{capture.JsonPath}': {ex.Message}");
                }
            }
        }
    }

    private void SetVariableFromResponse(string variableName, string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return;
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(variableName, out var value)) SetVariable(variableName, value.GetString());
        }
        catch (JsonException)
        {
            // A successful endpoint may legitimately return a non-JSON body; captures are handled explicitly by a later task.
        }
    }

    [GeneratedRegex("\\{\\{([^}]+)\\}\\}")]
    private static partial Regex VariablePattern();
}
