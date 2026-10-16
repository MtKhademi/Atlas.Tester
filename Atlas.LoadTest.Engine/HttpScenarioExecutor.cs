using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Atlas.LoadTest.Engine;

public sealed record ScenarioCredentials(string EmailOrPhone, string Password);

public sealed class HttpScenarioExecutor(HttpClient client, LoadTestScenario scenario, ScenarioCredentials credentials)
{
    private string? token;
    private string? resourceId;

    public async Task<IReadOnlyList<RequestMeasurement>> RunAsync(CancellationToken cancellationToken = default)
    {
        LoadTestLogging.Logger.Information("Scenario started: {Scenario}", scenario.Key);
        var results = new List<RequestMeasurement>();
        foreach (var step in scenario.Steps)
        {
            var path = step.Path.Replace("{id}", resourceId ?? "missing", StringComparison.Ordinal);
            using var request = new HttpRequestMessage(new HttpMethod(step.Method), path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (step.Name.Contains("login", StringComparison.OrdinalIgnoreCase))
                request.Content = JsonContent.Create(credentials);
            else if (step.Name.Contains("user asset", StringComparison.OrdinalIgnoreCase))
                request.Content = JsonContent.Create(new { marketAssetId = "0RWVV8YYAVVYW", quantity = 1.0m, averageUnitCost = 1000m, note = "load-test" });
            else if (step.Name.Contains("transaction", StringComparison.OrdinalIgnoreCase))
                request.Content = JsonContent.Create(new { quantity = 1.0m, unitPrice = 1000m, occurredAtUtc = DateTimeOffset.UtcNow });
            else if (step.Name.Contains("bank account", StringComparison.OrdinalIgnoreCase))
                request.Content = JsonContent.Create(new { bankCode = "MELLAT", accountNumber = "1234567890", iban = "IR820540102680020817909002", cardNumber = "6104337987654322", title = "Load test", isDefault = false });
            else if (step.Method is "POST" or "PUT" or "PATCH")
                request.Content = JsonContent.Create(new { price = 1234567.89m, symbol = "gold18" });

            var started = Stopwatch.GetTimestamp();
            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                var duration = Stopwatch.GetElapsedTime(started);
                if (step.Name.Contains("login", StringComparison.OrdinalIgnoreCase) && response.IsSuccessStatusCode)
                {
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                    if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("accessToken", out var accessToken)) token = accessToken.GetString();
                }
                if (response.IsSuccessStatusCode && resourceId is null)
                {
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                    if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("id", out var id)) resourceId = id.GetString();
                }
                results.Add(new(scenario.Key, step.Path, response.IsSuccessStatusCode, (int)response.StatusCode, duration));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                LoadTestLogging.Logger.Error(ex, "HTTP request failed: {Method} {Path}", step.Method, step.Path);
                results.Add(new(scenario.Key, step.Path, false, null, Stopwatch.GetElapsedTime(started), ex.GetType().Name));
            }
        }
        return results;
    }
}
