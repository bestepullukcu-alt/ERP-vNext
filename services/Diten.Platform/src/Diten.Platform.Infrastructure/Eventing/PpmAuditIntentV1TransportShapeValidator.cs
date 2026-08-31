using System.Globalization;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Contracts.Events;

namespace Diten.Platform.Infrastructure.Eventing;

/// <summary>
/// Performs only deterministic shape and binding checks after a future transport adapter has
/// authenticated the signed envelope. It deliberately does not provide an event key or register
/// a broker consumer.
/// </summary>
internal static class PpmAuditIntentV1TransportShapeValidator
{
    private static readonly HashSet<string> RequiredPayloadProperties =
        new(
            ["auditIntentId", "actorId", "entityId", "entityType", "mutation", "occurredAtUtc"],
            StringComparer.Ordinal);

    public static PpmAuditIntentV1ValidatedTransport Validate(
        Diten.BuildingBlocks.Eventing.EventTransportMessage message,
        PpmAuditIntentV1VerifiedTransportContext verifiedContext)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(verifiedContext);
        verifiedContext.Validate();

        if (!string.Equals(message.EventName, PpmAuditIntentSubmittedV1.CanonicalEventName, StringComparison.Ordinal)
            || message.EventVersion != PpmAuditIntentSubmittedV1.CanonicalEventVersion
            || !string.Equals(message.Producer, PpmAuditIntentV1AuditMapping.SourceService, StringComparison.Ordinal))
        {
            throw new EventSecurityException("PPM audit transport identity is not trusted.", "ppm.audit-intent.identity.invalid");
        }

        if (message.TenantId != verifiedContext.TenantId || message.TransportMetadata.Headers.Count != 3)
        {
            throw new EventSecurityException("PPM audit transport context is incomplete or mismatched.", "ppm.audit-intent.transport.invalid");
        }

        var auditIntent = ReadExactCanonicalPayload(message.CanonicalPayloadUtf8);
        if (message.EventId != auditIntent.AuditIntentId || auditIntent.ActorId != verifiedContext.ActorId)
        {
            throw new EventSecurityException("PPM audit payload does not agree with verified transport context.", "ppm.audit-intent.binding.invalid");
        }

        return new PpmAuditIntentV1ValidatedTransport(message, auditIntent, verifiedContext);
    }

    private static PpmAuditIntentSubmittedV1 ReadExactCanonicalPayload(ReadOnlyMemory<byte> canonicalPayloadUtf8)
    {
        try
        {
            using var document = JsonDocument.Parse(canonicalPayloadUtf8);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new EventValidationException("PPM audit payload must be a JSON object.");
            }

            var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!RequiredPayloadProperties.Contains(property.Name) || !properties.TryAdd(property.Name, property.Value))
                {
                    throw new EventValidationException("PPM audit payload properties are not exact.");
                }
            }

            if (properties.Count != RequiredPayloadProperties.Count
                || properties.Values.Any(value => value.ValueKind != JsonValueKind.String))
            {
                throw new EventValidationException("PPM audit payload properties are incomplete or invalid.");
            }

            if (!Guid.TryParseExact(properties["auditIntentId"].GetString(), "D", out var auditIntentId)
                || !Guid.TryParseExact(properties["actorId"].GetString(), "D", out var actorId)
                || !Guid.TryParseExact(properties["entityId"].GetString(), "D", out var entityId)
                || !DateTime.TryParseExact(
                    properties["occurredAtUtc"].GetString(),
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var occurredAtUtc)
                || occurredAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new EventValidationException("PPM audit payload identifiers or UTC timestamp are invalid.");
            }

            var auditIntent = new PpmAuditIntentSubmittedV1(
                auditIntentId,
                actorId,
                properties["entityType"].GetString()!,
                entityId,
                properties["mutation"].GetString()!,
                occurredAtUtc);
            var expected = ((ICanonicalIntegrationEvent)auditIntent).CanonicalPayloadUtf8;
            if (!canonicalPayloadUtf8.Span.SequenceEqual(expected.Span))
            {
                throw new EventValidationException("PPM audit payload is not canonical V1 UTF-8.");
            }

            return auditIntent;
        }
        catch (JsonException exception)
        {
            throw new EventValidationException($"PPM audit payload JSON is invalid: {exception.Message}");
        }
    }
}

/// <summary>
/// A future signature verifier creates this context only after authenticating the three trusted
/// transport headers. Keeping the input explicit prevents payload actor/tenant claims from being
/// treated as authorization evidence.
/// </summary>
internal sealed record PpmAuditIntentV1VerifiedTransportContext(Guid TenantId, Guid ActorId)
{
    public void Validate()
    {
        if (TenantId == Guid.Empty || ActorId == Guid.Empty)
        {
            throw new EventSecurityException("Verified PPM audit transport context is incomplete.", "ppm.audit-intent.context.invalid");
        }
    }
}

internal sealed record PpmAuditIntentV1ValidatedTransport(
    Diten.BuildingBlocks.Eventing.EventTransportMessage Message,
    PpmAuditIntentSubmittedV1 AuditIntent,
    PpmAuditIntentV1VerifiedTransportContext VerifiedContext);
