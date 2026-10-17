using System.Text;
using System.Text.Json;

namespace Atlas.Tester.Engine;

public sealed record ReportRow(string Scenario, string Endpoint, int Requests, int Success, int Errors, double AverageMilliseconds, double P95Milliseconds);
public sealed record RequestReportDetail(string Scenario, string Endpoint, string? Method, string? RequestUrl, string? RequestBody, int? StatusCode, string? ResponseBody, string? Error, double DurationMilliseconds, bool Succeeded);
public sealed record TesterReport(DateTimeOffset CompletedAtUtc, IReadOnlyList<ReportRow> Rows, IReadOnlyList<RequestReportDetail>? Details = null);

public static class TesterReportWriter
{
    public static async Task WriteJsonAsync(TesterReport report, string path, CancellationToken ct = default) =>
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), ct);

    public static async Task WriteCsvAsync(TesterReport report, string path, CancellationToken ct = default)
    {
        var lines = new List<string> { "Scenario,Endpoint,Requests,Success,Errors,AverageMilliseconds,P95Milliseconds" };
        lines.AddRange(report.Rows.Select(x => string.Join(',', x.Scenario, x.Endpoint, x.Requests, x.Success, x.Errors, x.AverageMilliseconds, x.P95Milliseconds)));
        await File.WriteAllLinesAsync(path, lines, ct);
    }

    public static async Task WriteRequestCsvAsync(TesterReport report, string path, CancellationToken ct = default)
    {
        var lines = new List<string> { "Scenario,Endpoint,Method,RequestUrl,RequestBody,StatusCode,ResponseBody,Error,DurationMilliseconds,Succeeded" };
        lines.AddRange((report.Details ?? []).Select(x => string.Join(',', Csv(x.Scenario), Csv(x.Endpoint), Csv(x.Method), Csv(x.RequestUrl), Csv(x.RequestBody), x.StatusCode?.ToString() ?? "", Csv(x.ResponseBody), Csv(x.Error), x.DurationMilliseconds, x.Succeeded)));
        await File.WriteAllLinesAsync(path, lines, ct);
    }

    public static async Task WriteHtmlAsync(TesterReport report, string path, CancellationToken ct = default)
    {
        var rows = string.Join(Environment.NewLine, report.Rows.Select(x => $"<tr><td>{Escape(x.Scenario)}</td><td>{Escape(x.Endpoint)}</td><td>{x.Requests}</td><td>{x.Success}</td><td>{x.Errors}</td><td>{x.AverageMilliseconds:0.##} ms</td><td>{x.P95Milliseconds:0.##} ms</td></tr>"));
        var details = string.Join(Environment.NewLine, (report.Details ?? []).Select((x, index) => $"<details><summary>#{index + 1} · {Escape(x.Method ?? "HTTP")} {Escape(x.RequestUrl ?? x.Endpoint)} · {(x.Succeeded ? "Passed" : "Failed")} · status: {x.StatusCode?.ToString() ?? "network"} · {x.DurationMilliseconds:0.##} ms</summary><h3>Request body</h3><pre>{Escape(x.RequestBody ?? "(empty)")}</pre><h3>Response body</h3><pre>{Escape(x.ResponseBody ?? "(empty)")}</pre><h3>Error</h3><pre>{Escape(x.Error ?? "(none)")}</pre></details>"));
        var html = $"<!doctype html><html><head><meta charset='utf-8'><title>Atlas Tester Report</title><style>body{{font-family:Arial;color:#10243e;padding:28px}}table{{border-collapse:collapse;width:100%}}th,td{{padding:10px;border-bottom:1px solid #deebf5;text-align:left}}th{{color:#70849a;background:#f5faff}}details{{margin-top:12px;border:1px solid #deebf5;padding:12px;border-radius:6px}}summary{{cursor:pointer;font-weight:bold}}pre{{white-space:pre-wrap;background:#f5faff;padding:10px}}</style></head><body><h1>Atlas Tester Report</h1><p>Completed: {report.CompletedAtUtc:O}</p><table><thead><tr><th>Scenario</th><th>Endpoint</th><th>Requests</th><th>Success</th><th>Errors</th><th>Average</th><th>P95</th></tr></thead><tbody>{rows}</tbody></table><h2>Request details</h2>{details}</body></html>";
        await File.WriteAllTextAsync(path, html, Encoding.UTF8, ct);
    }

    private static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value);
    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}
