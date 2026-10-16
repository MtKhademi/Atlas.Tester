namespace Atlas.LoadTest.Engine;

public enum TargetKind { Localhost, Remote }

public sealed record TargetValidationResult(Uri Target, TargetKind Kind, string DisplayTarget, bool RequiresConfirmation);

public static class TargetSafety
{
    public static TargetValidationResult Validate(string baseUrl, bool allowRemote, bool productionAcknowledged = false)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var target)
            || target.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(target.Host))
            throw new ArgumentException("Target URL must be an absolute HTTP or HTTPS URL.", nameof(baseUrl));

        var local = target.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || target.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || target.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);
        var kind = local ? TargetKind.Localhost : TargetKind.Remote;
        if (!local && !allowRemote) throw new InvalidOperationException("Remote target requires explicit opt-in.");
        if (productionAcknowledged && !allowRemote) throw new InvalidOperationException("Production acknowledgement requires remote opt-in.");
        return new(target, kind, target.GetLeftPart(UriPartial.Authority), !local || productionAcknowledged);
    }
}
