using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diten.Platform.API.Models.Workflow;

[JsonConverter(typeof(ApproveWorkflowTaskTransportRequestJsonConverter))]
public sealed record ApproveWorkflowTaskTransportRequest(
    string ReasonCode,
    string IdempotencyKey,
    string? Comment,
    string? EvidenceRef,
    string? ActorId = null,
    // Main sync 2026-10-13 — MOD-0022 E4 (ESIGN-BUNDLE x-workflow-addendum, main): the e-signature of a
    // signature-required step. Approve only; a reject body naming one is an unsupported field, as before.
    string? SignatureId = null);

[JsonConverter(typeof(RejectWorkflowTaskTransportRequestJsonConverter))]
public sealed record RejectWorkflowTaskTransportRequest(
    string ReasonCode,
    string IdempotencyKey,
    string? Comment,
    string? EvidenceRef,
    string? ActorId = null);

internal static class WorkflowTaskTransitionTransportJson
{
    private static readonly HashSet<string> KnownProperties = new(StringComparer.Ordinal)
    {
        "reasonCode", "idempotencyKey", "comment", "evidenceRef",
        // Carried ONLY so the controller can refuse a body that names somebody else (403 WORKFLOW_ACTOR_MISMATCH).
        // It is never the actor: that is the token's subject. A body may omit it or repeat the caller's own id.
        "actorId"
    };

    public static (string ReasonCode, string IdempotencyKey, string? Comment, string? EvidenceRef, string? ActorId, string? SignatureId) Read(
        ref Utf8JsonReader reader,
        bool allowsSignature = false)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A workflow task decision must be a JSON object.");
        }

        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!(KnownProperties.Contains(property.Name) || (allowsSignature && property.Name == "signatureId"))
                || !values.TryAdd(property.Name, property.Value.Clone()))
            {
                throw new JsonException("The workflow task decision contains an unsupported field.");
            }
        }

        if (!TryRequired(values, "reasonCode", 128, out var reasonCode)
            || !TryRequired(values, "idempotencyKey", 128, out var idempotencyKey)
            || !TryFreeText(values, "comment", 2000, out var comment)
            || !TryOptional(values, "evidenceRef", 512, out var evidenceRef)
            || !TryOptional(values, "actorId", 64, out var actorId)
            || !TryOptional(values, "signatureId", 64, out var signatureId))
        {
            throw new JsonException("The workflow task decision is invalid.");
        }

        return (reasonCode!, idempotencyKey!, comment, evidenceRef, actorId, signatureId);
    }

    public static void Write(
        Utf8JsonWriter writer,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string? evidenceRef,
        string? signatureId = null)
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

        if (signatureId is not null)
        {
            writer.WriteString("signatureId", signatureId);
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

    /// <summary>
    /// The comment is the one field a PERSON writes: a rejection reason is often more than one line, and people leave
    /// a space at either end. Line breaks and tabs are text here, and surrounding whitespace is carried through
    /// untouched — whether a comment is REQUIRED, and whether a blank one counts, is MOD-0023's answer
    /// (WORKFLOW_REJECT_COMMENT_REQUIRED), not the transport's. Every other control character is still refused, and
    /// the bound is unchanged. Codes and keys keep the exact, trimmed, no-control-character rule.
    /// </summary>
    private static bool TryFreeText(
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

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var candidate = element.GetString();
        if (candidate is null
            || candidate.Length > maximumLength
            || candidate.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
        {
            return false;
        }

        value = candidate;
        return true;
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
        var value = WorkflowTaskTransitionTransportJson.Read(ref reader, allowsSignature: true);
        return new(value.ReasonCode, value.IdempotencyKey, value.Comment, value.EvidenceRef, value.ActorId, value.SignatureId);
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
            value.EvidenceRef,
            value.SignatureId);
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
        return new(value.ReasonCode, value.IdempotencyKey, value.Comment, value.EvidenceRef, value.ActorId);
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
