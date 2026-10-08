namespace Diten.CrmService.Application.Features.CyclePeriod.Read;

/// <summary>
/// WP-CYC-UI-1 — the NARROW, read-only window onto the records that point AT a cycle period: its capacity, the campaigns
/// bound to it, the planning sessions planning it, and the planned visits those sessions committed.
/// <para><b>Why a seam instead of four repositories.</b> Each of those repositories carries write methods; a period
/// read handler holding them would put four foreign write paths one keystroke away. This interface cannot write, so
/// "the usage read changes nothing" is structural (the <see cref="ITerritoryBusinessUnitCatalog"/> precedent).</para>
/// <para><b>Visits are reached ONLY through the sessions.</b> A planned visit carries no period field; its link to a
/// period is the committing session's <c>CommittedPlannedVisitIds</c>. The reader therefore returns exactly the visits
/// those ids name, and never guesses a period from a visit's date.</para>
/// <para>Rows are raw on purpose: what counts (demand excludes cancelled / archived visits, which capacity is "the"
/// capacity) is decided in <c>CyclePeriodUsageRules</c>, where it can be tested.</para>
/// </summary>
public interface ICyclePeriodUsageReader
{
    /// <summary>Everything pointing at any of <paramref name="cyclePeriodIds"/>, in <paramref name="tenantId"/> only
    /// (non-deleted rows). One round of queries for the whole set — the list endpoint calls it once, not per row.</summary>
    Task<CyclePeriodUsageSnapshot> ReadAsync(
        Guid tenantId, IReadOnlyCollection<Guid> cyclePeriodIds, CancellationToken cancellationToken);
}

/// <summary>The raw rows pointing at a set of periods.</summary>
public sealed record CyclePeriodUsageSnapshot(
    IReadOnlyList<CyclePeriodUsageCapacityRow> Capacities,
    IReadOnlyList<CyclePeriodUsageCampaignRow> Campaigns,
    IReadOnlyList<CyclePeriodUsageSessionRow> Sessions,
    IReadOnlyList<CyclePeriodUsageVisitRow> Visits)
{
    public static readonly CyclePeriodUsageSnapshot Empty = new(
        Array.Empty<CyclePeriodUsageCapacityRow>(),
        Array.Empty<CyclePeriodUsageCampaignRow>(),
        Array.Empty<CyclePeriodUsageSessionRow>(),
        Array.Empty<CyclePeriodUsageVisitRow>());
}

public sealed record CyclePeriodUsageCapacityRow(Guid CycleCapacityId, Guid CyclePeriodId, bool IsArchived);

public sealed record CyclePeriodUsageCampaignRow(
    Guid CampaignId, Guid CyclePeriodId, string Code, string Name, string Status);

/// <summary>A planning session — no rep name: the owner's display name is resolved separately, for the owner only.</summary>
public sealed record CyclePeriodUsageSessionRow(
    Guid PlanningSessionId,
    Guid CyclePeriodId,
    string ResourceId,
    string Status,
    string? TargetWeekStart,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> CommittedPlannedVisitIds);

/// <summary>A committed planned visit — id, status and date only; no target, no person.</summary>
public sealed record CyclePeriodUsageVisitRow(Guid PlannedVisitId, string Status, DateOnly PlannedDate);
