using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Tester.Engine;

public static partial class JsonPathValueResolver
{
    public static string Resolve(JsonElement root, string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath) || !jsonPath.StartsWith('$'))
            throw new InvalidOperationException("JSONPath must start with '$'.");

        var current = root;
        var consumedLength = 1;
        foreach (Match segment in SegmentPattern().Matches(jsonPath))
        {
            if (segment.Index != consumedLength) throw new InvalidOperationException($"JSONPath '{jsonPath}' has an unsupported segment.");
            consumedLength += segment.Length;
            if (segment.Groups[1].Success)
            {
                var property = segment.Groups[1].Value;
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(property, out current))
                    throw new InvalidOperationException($"JSONPath '{jsonPath}' does not contain property '{property}'.");
            }
            else
            {
                var index = int.Parse(segment.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength())
                    throw new InvalidOperationException($"JSONPath '{jsonPath}' does not contain array index {index}.");
                current = current[index];
            }
        }

        if (consumedLength != jsonPath.Length) throw new InvalidOperationException($"JSONPath '{jsonPath}' has an unsupported segment.");
        return current.ValueKind == JsonValueKind.String ? current.GetString() ?? string.Empty : current.GetRawText();
    }

    [GeneratedRegex("\\.([A-Za-z_][A-Za-z0-9_]*)|\\[([0-9]+)\\]")]
    private static partial Regex SegmentPattern();
}
