using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diten.Platform.API.Models.Workflow;

[JsonConverter(typeof(ApproveWorkflowTaskTransportRequestJsonConverter))]
public sealed record ApproveWorkflowTaskTransportRequest(
    string ReasonCode,
    string IdempotencyKey,
    string? Comment,
    string? EvidenceRef);

[JsonConverter(typeof(RejectWorkflowTaskTransportRequestJsonConverter))]
public sealed record RejectWorkflowTaskTransportRequest(
    string ReasonCode,
    string IdempotencyKey,
    string? Comment,
    string? EvidenceRef);

internal static class WorkflowTaskTransitionTransportJson
{
    private static readonly HashSet<string> KnownProperties = new(StringComparer.Ordinal)
    {
        "reasonCode", "idempotencyKey", "comment", "evidenceRef"
    };

    public static (string ReasonCode, string IdempotencyKey, string? Comment, string? EvidenceRef) Read(
        ref Utf8JsonReader reader)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A workflow task decision must be a JSON object.");
        }

        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!KnownProperties.Contains(property.Name)
                || !values.TryAdd(property.Name, property.Value.Clone()))
            {
                throw new JsonException("The workflow task decision contains an unsupported field.");
            }
        }

        if (!TryRequired(values, "reasonCode", 128, out var reasonCode)
            || !TryRequired(values, "idempotencyKey", 128, out var idempotencyKey)
            || !TryOptional(values, "comment", 2000, out var comment)
            || !TryOptional(values, "evidenceRef", 512, out var evidenceRef))
        {
            throw new JsonException("The workflow task decision is invalid.");
        }

        return (reasonCode!, idempotencyKey!, comment, evidenceRef);
    }

    public static void Write(
        Utf8JsonWriter writer,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string? evidenceRef)
    {
        writer.WriteStartObject();
        writer.WriteString("reasonCode", reasonCode);
        writer.WriteString("idempotencyKey", idempotencyKey);
        if (comment is null)
        {
            writer.WriteNull("comment");
        }
        else
        {
            writer.WriteString("comment", comment);
        }

        if (evidenceRef is null)
        {
            writer.WriteNull("evidenceRef");
        }
        else
        {
            writer.WriteString("evidenceRef", evidenceRef);
        }

        writer.WriteEndObject();
    }

    private static bool TryRequired(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        int maximumLength,
        out string? value)
    {
        value = null;
        return values.TryGetValue(name, out var element)
            && element.ValueKind == JsonValueKind.String
            && IsBounded(element.GetString(), maximumLength, required: true, out value);
    }

    private static bool TryOptional(
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
            && IsBounded(element.GetString(), maximumLength, required: false, out value);
    }

    private static bool IsBounded(
        string? candidate,
        int maximumLength,
        bool required,
        out string? value)
    {
        value = null;
        if (candidate is null
            || (required && candidate.Length == 0)
            || candidate.Length > maximumLength
            || !string.Equals(candidate, candidate.Trim(), StringComparison.Ordinal)
            || candidate.Any(char.IsControl))
        {
            return false;
        }

        value = candidate;
        return true;
    }
}

internal sealed class ApproveWorkflowTaskTransportRequestJsonConverter
    : JsonConverter<ApproveWorkflowTaskTransportRequest>
{
    public override ApproveWorkflowTaskTransportRequest Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = WorkflowTaskTransitionTransportJson.Read(ref reader);
        return new(value.ReasonCode, value.IdempotencyKey, value.Comment, value.EvidenceRef);
    }

    public override void Write(
        Utf8JsonWriter writer,
        ApproveWorkflowTaskTransportRequest value,
        JsonSerializerOptions options) =>
        WorkflowTaskTransitionTransportJson.Write(
            writer,
            value.ReasonCode,
            value.IdempotencyKey,
            value.Comment,
            value.EvidenceRef);
}

internal sealed class RejectWorkflowTaskTransportRequestJsonConverter
    : JsonConverter<RejectWorkflowTaskTransportRequest>
{
    public override RejectWorkflowTaskTransportRequest Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = WorkflowTaskTransitionTransportJson.Read(ref reader);
        return new(value.ReasonCode, value.IdempotencyKey, value.Comment, value.EvidenceRef);
    }

    public override void Write(
        Utf8JsonWriter writer,
        RejectWorkflowTaskTransportRequest value,
        JsonSerializerOptions options) =>
        WorkflowTaskTransitionTransportJson.Write(
            writer,
            value.ReasonCode,
            value.IdempotencyKey,
            value.Comment,
            value.EvidenceRef);
}
