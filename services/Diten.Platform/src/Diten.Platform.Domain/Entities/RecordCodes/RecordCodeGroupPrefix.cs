using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §4.3 — the tenant's group prefix: a copy of the effective document <c>OrgPrefix</c>, taken by a platform
/// administrator's alignment and locked by the first Group allocation (RCS-001 §5.4). One row per tenant; never deleted.
/// </summary>
public sealed class RecordCodeGroupPrefix : TenantScopedEntity
{
    public required string GroupPrefix { get; init; }
    /// <summary>UTC (<see cref="DateTimeKind.Utc"/>); a BSON date. <c>DateTime</c>, not the pack §4 <c>DateTimeOffset</c>: owner decision B, 2026-11-02.</summary>
    public required DateTime AlignedAtUtc { get; init; }
    public required string AlignedBy { get; init; }

    /// <summary>Set once, by the first Group allocation, in that allocation's transaction. Filled = locked. UTC, as above.</summary>
    public DateTime? LockedAtUtc { get; init; }
}
