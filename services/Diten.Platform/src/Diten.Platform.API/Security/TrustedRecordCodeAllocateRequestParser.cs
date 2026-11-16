using System.Net.Http.Headers;
using System.Text.Json;
using Diten.Platform.Application.Features.RecordCodes;
using Diten.Platform.Domain.Enums.RecordCodes;
using Diten.Platform.Domain.Features.RecordCodes;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0040-FU02 §8.5 step 5 / §12 — the allocate body, read RAW (no model binding, no validator): <c>application/json</c>,
/// one JSON object, exactly the four contract members, none twice. The verdict is passed to the handler, which gives — and
/// records — the 400 (a validator's refusal would never reach the audit trail). The default scope is applied here, so
/// a v0 body and an explicit <c>"LegalEntity"</c> are the same request.
/// </summary>
public static class TrustedRecordCodeAllocateRequestParser
{
    public const int MaxIdempotencyKeyLength = 200;

    private static readonly HashSet<string> Members = new(StringComparer.Ordinal)
    {
        "recordType", "prefixScope", "legalEntityId", "idempotencyKey"
    };

    public static TrustedRecordCodeAllocateParseResult Parse(string? contentType, ReadOnlyMemory<byte> body)
    {
        if (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType)
            || !string.Equals(mediaType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid("Content-Type must be application/json.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return Invalid("The body is not JSON.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Invalid("The body must be a JSON object.");
            }

            var seen = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var member in document.RootElement.EnumerateObject())
            {
                if (!Members.Contains(member.Name))
                {
                    return Invalid($"Unknown member '{member.Name}'.");
                }

                if (!seen.TryAdd(member.Name, member.Value))
                {
                    return Invalid($"Member '{member.Name}' appears more than once.");
                }
            }

            if (!seen.TryGetValue("recordType", out var recordTypeElement)
                || recordTypeElement.ValueKind != JsonValueKind.String
                || !RecordCodeFormat.IsValidRecordType(recordTypeElement.GetString()))
            {
                return Invalid("recordType must be three ASCII capitals.");
            }

            var scope = RecordCodePrefixScope.LegalEntity;
            if (seen.TryGetValue("prefixScope", out var scopeElement) && scopeElement.ValueKind != JsonValueKind.Null)
            {
                var name = scopeElement.ValueKind == JsonValueKind.String ? scopeElement.GetString() : null;
                switch (name)
                {
                    case "LegalEntity":
                        scope = RecordCodePrefixScope.LegalEntity;
                        break;
                    case "Group":
                        scope = RecordCodePrefixScope.Group;
                        break;
                    case "Platform":
                        scope = RecordCodePrefixScope.Platform;
                        break;
                    default:
                        return Invalid("prefixScope must be LegalEntity, Group or Platform.");
                }
            }

            Guid? legalEntityId = null;
            if (seen.TryGetValue("legalEntityId", out var legalEntityElement) && legalEntityElement.ValueKind != JsonValueKind.Null)
            {
                if (legalEntityElement.ValueKind != JsonValueKind.String
                    || !Guid.TryParse(legalEntityElement.GetString(), out var parsed))
                {
                    return Invalid("legalEntityId must be a UUID or null.");
                }

                legalEntityId = parsed;
            }

            if (!seen.TryGetValue("idempotencyKey", out var keyElement)
                || keyElement.ValueKind != JsonValueKind.String
                || !IsValidIdempotencyKey(keyElement.GetString()))
            {
                return Invalid($"idempotencyKey must be 1–{MaxIdempotencyKeyLength} printable ASCII characters (! to ~).");
            }

            return new TrustedRecordCodeAllocateParseResult(
                new AllocateRecordCodeRequest(recordTypeElement.GetString()!, scope, legalEntityId, keyElement.GetString()!),
                []);
        }
    }

    private static bool IsValidIdempotencyKey(string? key) =>
        key is { Length: > 0 and <= MaxIdempotencyKeyLength } && key.All(character => character is >= '!' and <= '~');

    private static TrustedRecordCodeAllocateParseResult Invalid(string error) => new(null, [error]);
}

public sealed record TrustedRecordCodeAllocateParseResult(AllocateRecordCodeRequest? Request, IReadOnlyList<string> Errors);
