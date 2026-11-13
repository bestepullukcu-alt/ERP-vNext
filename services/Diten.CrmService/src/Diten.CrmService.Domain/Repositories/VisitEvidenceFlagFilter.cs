namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// WP-VW-W3E (BE-b, §2.6) — the manager's evidence flags read over <c>visit_executions</c>, pushed to the database:
/// the start window [<see cref="FromInclusive"/>, <see cref="ToExclusive"/>) (server UTC instants); the reps the caller
/// may see (<see cref="ResourceIds"/> null = the whole tenant, read-all); any of <see cref="AnyFlags"/>; only flagged
/// starts; the plans of one institution (<see cref="PlannedVisitIds"/>, resolved by the caller).
/// </summary>
public sealed record VisitEvidenceFlagFilter(
    DateTimeOffset FromInclusive,
    DateTimeOffset ToExclusive,
    IReadOnlyCollection<string>? ResourceIds,
    IReadOnlyCollection<string>? AnyFlags,
    bool OnlyFlagged,
    IReadOnlyCollection<Guid>? PlannedVisitIds);
