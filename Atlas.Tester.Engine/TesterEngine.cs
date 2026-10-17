namespace Atlas.Tester.Engine;

public sealed record TesterOptions(int Threads, int Iterations, TimeSpan RampUp, TimeSpan RequestTimeout)
{
    public void Validate()
    {
        if (Threads is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(Threads));
        if (Iterations is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(Iterations));
        if (RampUp < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RampUp));
        if (RequestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
    }
}

public sealed record RequestMeasurement(
    string Scenario,
    string Endpoint,
    bool Succeeded,
    int? StatusCode,
    TimeSpan Duration,
    string? Error = null,
    string? Method = null,
    string? RequestUrl = null,
    string? RequestBody = null,
    string? ResponseBody = null);
public sealed record TesterSummary(int Total, int Succeeded, int Failed, double RequestsPerSecond, TimeSpan AverageLatency, TimeSpan P95Latency, IReadOnlyDictionary<string, int> StatusCodes);

public sealed class TesterRunner
{
    public async Task<TesterSummary> RunAsync(TesterOptions options, Func<CancellationToken, Task<RequestMeasurement>> workload, IProgress<RequestMeasurement>? progress = null, CancellationToken cancellationToken = default)
    {
        options.Validate();
        var measurements = new System.Collections.Concurrent.ConcurrentQueue<RequestMeasurement>();
        var started = DateTimeOffset.UtcNow;
        var delay = options.Threads == 1 ? options.RampUp : TimeSpan.FromTicks(options.RampUp.Ticks / options.Threads);
        await Parallel.ForEachAsync(Enumerable.Range(0, options.Threads), cancellationToken, async (worker, ct) =>
        {
            if (delay > TimeSpan.Zero) await Task.Delay(TimeSpan.FromTicks(delay.Ticks * worker), ct);
            for (var i = 0; i < options.Iterations; i++)
            {
                var measurement = await workload(ct);
                measurements.Enqueue(measurement);
                progress?.Report(measurement);
            }
        });
        var all = measurements.ToArray();
        var latencies = all.Select(x => x.Duration).OrderBy(x => x).ToArray();
        var p95 = latencies.Length == 0 ? TimeSpan.Zero : latencies[Math.Min(latencies.Length - 1, (int)Math.Ceiling(latencies.Length * .95) - 1)];
        return new(all.Length, all.Count(x => x.Succeeded), all.Count(x => !x.Succeeded), all.Length / Math.Max(.001, (DateTimeOffset.UtcNow - started).TotalSeconds), all.Length == 0 ? TimeSpan.Zero : TimeSpan.FromTicks((long)all.Average(x => x.Duration.Ticks)), p95, all.Where(x => x.StatusCode.HasValue).GroupBy(x => x.StatusCode!.Value.ToString()).ToDictionary(x => x.Key, x => x.Count()));
    }

    public async Task<IReadOnlyList<RequestMeasurement>> RunScenarioAsync(
        TesterOptions options,
        Func<CancellationToken, Task<IReadOnlyList<RequestMeasurement>>> workload,
        IProgress<RequestMeasurement>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        TesterLogging.Logger.Information("Performance test started: threads={Threads}, iterations={Iterations}, rampUp={RampUp}", options.Threads, options.Iterations, options.RampUp);
        var measurements = new System.Collections.Concurrent.ConcurrentQueue<RequestMeasurement>();
        var delay = options.Threads == 1 ? options.RampUp : TimeSpan.FromTicks(options.RampUp.Ticks / options.Threads);
        await Parallel.ForEachAsync(Enumerable.Range(0, options.Threads), cancellationToken, async (worker, ct) =>
        {
            if (delay > TimeSpan.Zero) await Task.Delay(TimeSpan.FromTicks(delay.Ticks * worker), ct);
            for (var i = 0; i < options.Iterations; i++)
            {
                foreach (var measurement in await workload(ct))
                {
                    TesterLogging.Logger.Information("Request {Scenario} {Endpoint}: success={Succeeded}, status={StatusCode}, durationMs={DurationMs}", measurement.Scenario, measurement.Endpoint, measurement.Succeeded, measurement.StatusCode, measurement.Duration.TotalMilliseconds);
                    measurements.Enqueue(measurement);
                    progress?.Report(measurement);
                }
            }
        });
        TesterLogging.Logger.Information("Performance test completed: measurements={Count}", measurements.Count);
        return measurements.ToArray();
    }
}
