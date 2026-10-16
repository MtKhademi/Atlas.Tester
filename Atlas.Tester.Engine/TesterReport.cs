using System.Text;
using System.Text.Json;

namespace Atlas.Tester.Engine;

public sealed record ReportRow(string Scenario, string Endpoint, int Requests, int Success, int Errors, double AverageMilliseconds, double P95Milliseconds);
public sealed record TesterReport(DateTimeOffset CompletedAtUtc, IReadOnlyList<ReportRow> Rows);

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

    public static async Task WriteHtmlAsync(TesterReport report, string path, CancellationToken ct = default)
    {
        var rows = string.Join(Environment.NewLine, report.Rows.Select(x => $"<tr><td>{Escape(x.Scenario)}</td><td>{Escape(x.Endpoint)}</td><td>{x.Requests}</td><td>{x.Success}</td><td>{x.Errors}</td><td>{x.AverageMilliseconds:0.##} ms</td><td>{x.P95Milliseconds:0.##} ms</td></tr>"));
        var html = $"<!doctype html><html><head><meta charset='utf-8'><title>Atlas Tester Report</title><style>body{{font-family:Arial;color:#10243e;padding:28px}}table{{border-collapse:collapse;width:100%}}th,td{{padding:10px;border-bottom:1px solid #deebf5;text-align:left}}th{{color:#70849a;background:#f5faff}}</style></head><body><h1>Atlas Tester Report</h1><p>Completed: {report.CompletedAtUtc:O}</p><table><thead><tr><th>Scenario</th><th>Endpoint</th><th>Requests</th><th>Success</th><th>Errors</th><th>Average</th><th>P95</th></tr></thead><tbody>{rows}</tbody></table></body></html>";
        await File.WriteAllTextAsync(path, html, Encoding.UTF8, ct);
    }

    private static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value);
}
