using System.Text.Json;

namespace Diten.Platform.API.Models.Audit;

public sealed class TrustedSourceAuditIntentRequestParser
{
    private static readonly HashSet<string> KnownProperties = new(StringComparer.Ordinal)
    {
        "sourceService", "contractVersion", "intentId", "tenantId", "aggregateType", "aggregateId",
        "preVersion", "postVersion", "operation", "actorId", "correlationId", "causationId", "commandId",
        "sequence", "timestampUtc", "evidenceHash", "snapshotReference", "idempotencyKey"
    };

    public bool TryParse(ReadOnlyMemory<byte> utf8Json, out TrustedSourceAuditIntentRequest? request)
    {
        request = null;
        try
        {
            using var document = JsonDocument.Parse(utf8Json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!KnownProperties.Contains(property.Name) || !values.TryAdd(property.Name, property.Value))
                {
                    return false;
                }
            }

            if (values.Count is not (17 or 18)
                || !TryString(values, "sourceService", out var sourceService)
                || !TryString(values, "contractVersion", out var contractVersion)
                || !TryGuid(values, "intentId", out var intentId)
                || !TryGuid(values, "tenantId", out var tenantId)
                || !TryString(values, "aggregateType", out var aggregateType)
                || !TryGuid(values, "aggregateId", out var aggregateId)
                || !TryInt32(values, "preVersion", out var preVersion)
                || !TryInt32(values, "postVersion", out var postVersion)
                || !TryString(values, "operation", out var operation)
                || !TryString(values, "actorId", out var actorId)
                || !TryGuid(values, "correlationId", out var correlationId)
                || !TryString(values, "causationId", out var causationId)
                || !TryString(values, "commandId", out var commandId)
                || !TryInt64(values, "sequence", out var sequence)
                || !TryDateTimeOffset(values, "timestampUtc", out var timestampUtc)
                || !TryString(values, "evidenceHash", out var evidenceHash)
                || !TryNullableString(values, "snapshotReference", out var snapshotReference)
                || !TryString(values, "idempotencyKey", out var idempotencyKey))
            {
                return false;
            }

            request = new TrustedSourceAuditIntentRequest(
                sourceService!, contractVersion!, intentId, tenantId, aggregateType!, aggregateId,
                preVersion, postVersion, operation!, actorId!, correlationId, causationId!, commandId!,
                sequence, timestampUtc, evidenceHash!, snapshotReference, idempotencyKey!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryString(IReadOnlyDictionary<string, JsonElement> values, string name, out string? value)
    {
        value = null;
        if (!values.TryGetValue(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return value is not null;
    }

    private static bool TryNullableString(IReadOnlyDictionary<string, JsonElement> values, string name, out string? value)
    {
        value = null;
        if (!values.TryGetValue(name, out var element))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return true;
    }

    private static bool TryGuid(IReadOnlyDictionary<string, JsonElement> values, string name, out Guid value)
    {
        value = Guid.Empty;
        return values.TryGetValue(name, out var element)
               && element.ValueKind == JsonValueKind.String
               && element.TryGetGuid(out value);
    }

    private static bool TryInt32(IReadOnlyDictionary<string, JsonElement> values, string name, out int value)
    {
        value = default;
        return values.TryGetValue(name, out var element)
               && element.ValueKind == JsonValueKind.Number
               && element.TryGetInt32(out value);
    }

    private static bool TryInt64(IReadOnlyDictionary<string, JsonElement> values, string name, out long value)
    {
        value = default;
        return values.TryGetValue(name, out var element)
               && element.ValueKind == JsonValueKind.Number
               && element.TryGetInt64(out value);
    }

    private static bool TryDateTimeOffset(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        out DateTimeOffset value)
    {
        value = default;
        return values.TryGetValue(name, out var element)
               && element.ValueKind == JsonValueKind.String
               && element.TryGetDateTimeOffset(out value);
    }
}
