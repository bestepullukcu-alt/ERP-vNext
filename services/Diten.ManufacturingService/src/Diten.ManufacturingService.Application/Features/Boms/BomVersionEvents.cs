using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.ManufacturingService.Domain.Entities;

namespace Diten.ManufacturingService.Application.Features.Boms;

/// <summary>
/// BOM-version lifecycle events — <c>bom.openapi.yaml</c> v1.2.0 <c>webhooks.bomVersionLifecycleEvent</c> /
/// <c>BomVersionEvent</c>. Identity and state only: no component, quantity or routing line travels in the event; the
/// consumer (MOD-0189 MRP) reads the structure through <c>current</c> / <c>explode</c>.
/// </summary>
public static class BomVersionEvents
{
    public const string EffectiveEventName = "manufacturing.bomversion.effective.v1";
    public const string SupersededEventName = "manufacturing.bomversion.superseded.v1";
    public const string Producer = "Diten.ManufacturingService";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>The version just released (now Effective).</summary>
    public static BomOutboxEvent Effective(BomVersion bom, string correlationId, DateTimeOffset now) =>
        Create(bom, EffectiveEventName, "BomVersionEffective", correlationId, now);

    /// <summary>The version that was Effective until this release (now Superseded).</summary>
    public static BomOutboxEvent Superseded(BomVersion bom, string correlationId, DateTimeOffset now) =>
        Create(bom, SupersededEventName, "BomVersionSuperseded", correlationId, now);

    private static BomOutboxEvent Create(BomVersion bom, string eventName, string eventType, string correlationId, DateTimeOffset now)
    {
        if (bom.TenantId is not { } tenantId || bom.LegalEntityId is not { } legalEntityId || bom.EffectiveFrom is not { } effectiveFrom)
        {
            throw new InvalidOperationException("A BOM-version event needs the version's tenant, legal entity and effectiveFrom.");
        }

        var eventId = Guid.NewGuid();
        // The correlation middleware only admits a UUID (or mints one); anything else is a programming error upstream.
        var correlation = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();
        var payload = new BomVersionEventPayload(
            eventId,
            eventType,
            now,
            correlation,
            tenantId,
            legalEntityId,
            bom.ItemId,
            bom.Id,
            bom.RevisionNo,
            bom.Status.ToString(),
            effectiveFrom,
            "v1");

        return new BomOutboxEvent
        {
            Id = eventId,
            EventName = eventName,
            EventVersion = 1,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            BomVersionId = bom.Id,
            CorrelationId = correlation,
            OccurredAtUtc = now,
            PayloadJson = JsonSerializer.Serialize(payload, Json),
            Status = BomOutboxStatus.Pending,
            NextAttemptAtUtc = now
        };
    }
}

/// <summary>Wire shape of <c>BomVersionEvent</c> (camelCase). Field set is pinned by <c>BomVersionEventContractTests</c>.</summary>
public sealed record BomVersionEventPayload(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    Guid CorrelationId,
    Guid TenantId,
    Guid LegalEntityId,
    Guid ItemId,
    Guid BomVersionId,
    int Version,
    string Status,
    DateTimeOffset EffectiveFrom,
    string ContractVersion);
