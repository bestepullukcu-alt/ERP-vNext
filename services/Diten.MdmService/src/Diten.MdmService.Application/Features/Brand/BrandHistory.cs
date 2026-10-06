using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.Brand;

/// <summary>WP-MDM-BRAND-ACCEPT-01 FIX1 (item 1) — builds the one append-only entry each brand write leaves.</summary>
public static class BrandHistory
{
    public static BrandHistoryEntry Entry(
        string eventType, string? actor, string? correlationId, Domain.Entities.Brand? before, Domain.Entities.Brand after)
        => Entry(eventType, actor, correlationId, before is null ? null : BrandAuditSnapshot.Of(before), after);

    public static BrandHistoryEntry Entry(
        string eventType, string? actor, string? correlationId, BrandAuditSnapshot? before, Domain.Entities.Brand after)
        => new()
        {
            EventType = eventType,
            Actor = actor,
            // A request without X-Correlation-Id still gets a value, so no entry is ever uncorrelated.
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim(),
            Before = before,
            After = BrandAuditSnapshot.Of(after),
            OccurredAtUtc = DateTimeOffset.UtcNow
        };
}

/// <summary>The stored state of a brand, taken before a write mutates the loaded instance.</summary>
public static class BrandAuditSnapshotSource
{
    public static BrandAuditSnapshot Copy(Domain.Entities.Brand brand) => BrandAuditSnapshot.Of(brand);
}
