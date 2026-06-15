using System.Text.Json;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed class JsonBusinessReferenceDataImportParser : IBusinessReferenceDataImportParser
{
    public string ParserKey => "json-v1";

    public bool Supports(string format, string fileName)
    {
        var normalized = format.Trim().ToLowerInvariant();
        return normalized is "json" or "application/json" || fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<BusinessReferenceDataImportParsedRow>> ParseAsync(byte[] content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // JsonDocument.Parse handles a UTF-8 BOM in the byte payload directly.
        using var document = JsonDocument.Parse(content);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("invalid_import_payload_json_array_required");
        }

        var rows = new List<BusinessReferenceDataImportParsedRow>();
        var i = 0;
        foreach (var element in document.RootElement.EnumerateArray())
        {
            i++;
            var valueCode = GetString(element, "value_code");
            var displayName = GetString(element, "display_name");
            var description = GetString(element, "description");
            var parent = GetString(element, "parent_value_code");
            var replacement = GetString(element, "replacement_value_code");
            var isDeprecated = GetBool(element, "is_deprecated");
            var sortOrder = GetInt(element, "sort_order") ?? 0;
            var attributes = GetAttributes(element, "attributes");

            rows.Add(new BusinessReferenceDataImportParsedRow(
                i,
                valueCode,
                displayName,
                description,
                parent,
                replacement,
                isDeprecated,
                sortOrder,
                attributes));
        }

        return Task.FromResult<IReadOnlyList<BusinessReferenceDataImportParsedRow>>(rows);
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
        {
            return null;
        }

        return prop.ValueKind == JsonValueKind.Null ? null : prop.GetString();
    }

    private static bool GetBool(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(prop.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private static int? GetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var n))
        {
            return n;
        }

        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static Dictionary<string, string>? GetAttributes(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (prop.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in prop.EnumerateObject())
        {
            dict[child.Name] = child.Value.ValueKind == JsonValueKind.String
                ? child.Value.GetString() ?? string.Empty
                : child.Value.ToString();
        }

        return dict;
    }
}
