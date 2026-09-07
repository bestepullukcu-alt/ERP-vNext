using System.Text.Json;

namespace Diten.Platform.API.Models.Workflow;

public sealed class TrustedWorkflowConsumerRequestParser
{
    private static readonly HashSet<string> StartProperties = new(StringComparer.Ordinal)
    {
        "templateId",
        "templateCode",
        "objectType",
        "objectId",
        "objectRef",
        "candidatePrincipalIds",
        "reasonCode",
        "commentRequired",
        "evidenceRequired",
        "dueAt"
    };

    private static readonly HashSet<string> EvidenceProperties = new(StringComparer.Ordinal)
    {
        "workflowInstanceId",
        "expectedObjectType",
        "expectedObjectId"
    };

    private static readonly HashSet<string> StartResultProperties = new(StringComparer.Ordinal)
    {
        "expectedObjectType",
        "expectedObjectId",
        "expectedMakerSubjectId"
    };

    public bool TryParseStart(
        ReadOnlyMemory<byte> utf8Json,
        out TrustedWorkflowStartTransportRequest? request)
    {
        request = null;
        if (!TryReadObject(utf8Json, StartProperties, out var values)
            || !TryNullableGuid(values, "templateId", out var templateId)
            || !TryNullableBoundedString(values, "templateCode", 128, out var templateCode)
            || !TryRequiredBoundedString(values, "objectType", 128, out var objectType)
            || !TryRequiredBoundedString(values, "objectId", 256, out var objectId)
            || !TryNullableBoundedString(values, "objectRef", 512, out var objectRef)
            || !TryStringArray(values, "candidatePrincipalIds", 100, 256, out var candidatePrincipalIds)
            || !TryNullableBoundedString(values, "reasonCode", 128, out var reasonCode)
            || !TryBoolean(values, "commentRequired", out var commentRequired)
            || !TryBoolean(values, "evidenceRequired", out var evidenceRequired)
            || !TryNullableUtcDateTimeOffset(values, "dueAt", out var dueAt)
            || (templateId.HasValue == (templateCode is not null)))
        {
            return false;
        }

        request = new(
            templateId,
            templateCode,
            objectType!,
            objectId!,
            objectRef,
            candidatePrincipalIds!,
            reasonCode,
            commentRequired,
            evidenceRequired,
            dueAt);
        return true;
    }

    public bool TryParseEvidence(
        ReadOnlyMemory<byte> utf8Json,
        out TrustedWorkflowTerminalDecisionEvidenceTransportRequest? request)
    {
        request = null;
        if (!TryReadObject(utf8Json, EvidenceProperties, out var values)
            || values.Count != 3
            || !TryRequiredGuid(values, "workflowInstanceId", out var workflowInstanceId)
            || !TryRequiredBoundedString(values, "expectedObjectType", 128, out var expectedObjectType)
            || !TryRequiredBoundedString(values, "expectedObjectId", 256, out var expectedObjectId))
        {
            return false;
        }

        request = new(workflowInstanceId, expectedObjectType!, expectedObjectId!);
        return true;
    }

    public bool TryParseStartResult(
        ReadOnlyMemory<byte> utf8Json,
        out TrustedWorkflowStartResultTransportRequest? request)
    {
        request = null;
        if (!TryReadObject(utf8Json, StartResultProperties, out var values)
            || values.Count != 3
            || !TryRequiredBoundedString(values, "expectedObjectType", 128, out var expectedObjectType)
            || !TryRequiredBoundedString(values, "expectedObjectId", 256, out var expectedObjectId)
            || !TryRequiredGuid(values, "expectedMakerSubjectId", out var expectedMakerSubjectId))
        {
            return false;
        }

        request = new(expectedObjectType!, expectedObjectId!, expectedMakerSubjectId);
        return true;
    }

    private static bool TryReadObject(
        ReadOnlyMemory<byte> utf8Json,
        IReadOnlySet<string> knownProperties,
        out IReadOnlyDictionary<string, JsonElement> values)
    {
        values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
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

            var parsed = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!knownProperties.Contains(property.Name)
                    || !parsed.TryAdd(property.Name, property.Value.Clone()))
                {
                    return false;
                }
            }

            values = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryRequiredBoundedString(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        int maximumLength,
        out string? value)
    {
        value = null;
        return values.TryGetValue(name, out var element)
            && element.ValueKind == JsonValueKind.String
            && IsExactBoundedString(element.GetString(), maximumLength, out value);
    }

    private static bool TryNullableBoundedString(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        int maximumLength,
        out string? value)
    {
        value = null;
        if (!values.TryGetValue(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        return element.ValueKind == JsonValueKind.String
            && IsExactBoundedString(element.GetString(), maximumLength, out value);
    }

    private static bool IsExactBoundedString(string? candidate, int maximumLength, out string? value)
    {
        value = null;
        if (candidate is not { Length: > 0 }
            || candidate.Length > maximumLength
            || !string.Equals(candidate, candidate.Trim(), StringComparison.Ordinal)
            || candidate.Any(char.IsControl))
        {
            return false;
        }

        value = candidate;
        return true;
    }

    private static bool TryStringArray(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        int maximumCount,
        int maximumItemLength,
        out IReadOnlyList<string>? result)
    {
        result = null;
        if (!values.TryGetValue(name, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var items = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || !IsExactBoundedString(item.GetString(), maximumItemLength, out var value)
                || !unique.Add(value!))
            {
                return false;
            }

            items.Add(value!);
            if (items.Count > maximumCount)
            {
                return false;
            }
        }

        if (items.Count == 0)
        {
            return false;
        }

        items.Sort(StringComparer.Ordinal);
        result = items;
        return true;
    }

    private static bool TryNullableGuid(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        out Guid? value)
    {
        value = null;
        if (!values.TryGetValue(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String
            || !Guid.TryParseExact(element.GetString(), "D", out var parsed)
            || parsed == Guid.Empty)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryRequiredGuid(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        out Guid value)
    {
        value = Guid.Empty;
        return values.TryGetValue(name, out var element)
            && element.ValueKind == JsonValueKind.String
            && Guid.TryParseExact(element.GetString(), "D", out value)
            && value != Guid.Empty;
    }

    private static bool TryBoolean(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        out bool value)
    {
        value = default;
        if (!values.TryGetValue(name, out var element)
            || element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = element.GetBoolean();
        return true;
    }

    private static bool TryNullableUtcDateTimeOffset(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        out DateTimeOffset? value)
    {
        value = null;
        if (!values.TryGetValue(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String
            || !element.TryGetDateTimeOffset(out var parsed)
            || parsed.Offset != TimeSpan.Zero)
        {
            return false;
        }

        value = parsed;
        return true;
    }
}
