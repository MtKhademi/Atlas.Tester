using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Tester.Engine;

public sealed record SmsMockMessage(string Recipient, int TemplateId, IReadOnlyList<string> ParameterNames, string CorrelationId, DateTimeOffset QueuedAtUtc, string Status);
public interface ISmsMockActivity { bool HasQueuedMessage(string recipient, string? templateId); IReadOnlyList<SmsMockMessage> Messages { get; } }

public sealed class SmsMockServer : IAsyncDisposable, ISmsMockActivity
{
    private readonly ConcurrentQueue<SmsMockMessage> messages = new();
    private WebApplication? app;
    public Uri BaseAddress { get; private set; } = null!;
    public IReadOnlyList<SmsMockMessage> Messages => messages.ToArray();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(0, listen => listen.UseHttps()));
        app = builder.Build();
        app.MapPost("/v1/send/verify", async (HttpRequest request) =>
        {
            using var document = await JsonDocument.ParseAsync(request.Body);
            var root = document.RootElement;
            var mobile = root.TryGetProperty("mobile", out var mobileValue) ? mobileValue.GetString() ?? "" : "";
            var template = root.TryGetProperty("templateId", out var templateValue) && templateValue.TryGetInt32(out var templateId) ? templateId : 0;
            var names = root.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Array
                ? parameters.EnumerateArray().Where(item => item.TryGetProperty("name", out _)).Select(item => item.GetProperty("name").GetString() ?? "").ToArray() : [];
            var correlationId = Guid.NewGuid().ToString("N");
            messages.Enqueue(new SmsMockMessage(mobile, template, names, correlationId, DateTimeOffset.UtcNow, "queued"));
            return Results.Json(new { status = 1, data = new { messageId = long.Parse(correlationId[..12], System.Globalization.NumberStyles.HexNumber) } });
        });
        await app.StartAsync(cancellationToken);
        var port = app.Urls.Select(url => new Uri(url).Port).FirstOrDefault();
        if (port == 0) throw new InvalidOperationException("SMS mock did not expose an HTTPS port.");
        BaseAddress = new Uri($"https://localhost:{port}/");
    }

    public bool HasQueuedMessage(string recipient, string? templateId)
        => messages.Any(message => string.Equals(message.Recipient, recipient.TrimStart('+').Replace("98", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(templateId) || string.Equals(message.TemplateId.ToString(), templateId, StringComparison.OrdinalIgnoreCase)));

    public async ValueTask DisposeAsync() { if (app is not null) await app.DisposeAsync(); }
}
