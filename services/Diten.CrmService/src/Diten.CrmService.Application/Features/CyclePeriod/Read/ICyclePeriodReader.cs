using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Features.CyclePeriod.Read;

/// <summary>
/// MOD-0165 FU06/FU07 — the single READ-ONLY consumption seam for "which period?". MOD-0155 (MicroTarget) is its first
/// consumer; the HTTP endpoint consumes the very same seam, so there is exactly one implementation of the rule and no
/// consumer re-implements "active + covering window + precedence".
/// <para><b>Read-only, in-process, no self-call.</b> No method here writes: the implementation never calls
/// <c>InsertAsync</c> / <c>ReplaceAsync</c> and holds no <c>HttpClient</c> — a consumer inside CrmService must not go
/// out through the Gateway to reach its own service (MOD-0165 FU03's rule, kept). It also holds no legal-entity
/// validator: proving an MDM reference is a WRITE-path concern, and a read must not fail because another service is
/// down.</para>
/// <para><b>The tenant is the caller's tenant.</b> The seam resolves it from the request context and never selects one
/// of its own, so a consumer cannot read another tenant's calendar through it.</para>
/// <para><b>FU07 widened the resolve signature, and deliberately did not overload it.</b> Two signatures would be two
/// precedence behaviours, and a consumer that called the wrong one would be wrong silently. Backward compatibility is
/// kept in the SEMANTICS instead: pass only an instant and a business unit — the FU06 shape — and unnamed levels are
/// skipped, so the answer is identical to FU06's for ever.</para>
/// </summary>
public interface ICyclePeriodReader
{
    /// <summary>
    /// Which period is in force at <paramref name="at"/>, at the most specific address the caller named? Walks
    /// business-unit → legal-entity → country → tenant, skipping levels the caller left null, and answers from the
    /// first level that has a covering row. Returns resolved / none / ambiguous — never a guess, never the nearest
    /// period, and never a merge of two levels.
    /// </summary>
    Task<CyclePeriodResolution> ResolveActiveAsync(
        DateTimeOffset at,
        string? country,
        Guid? legalEntityId,
        string? businessUnitId,
        CancellationToken cancellationToken);

    /// <summary>One period by id, or <c>null</c> when it does not exist in the caller's tenant.</summary>
    Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken cancellationToken);

    /// <summary>
    /// MOD-0165 FU08 — several periods by id, in one round trip. Added for the campaign list, which shows the bound
    /// period's code next to each campaign: reading them one at a time would be an N+1 over a grid.
    /// <para>Like every other method here it is READ-ONLY and tenant-scoped. Ids that do not exist in the caller's
    /// tenant are simply absent from the result — this is a lookup, not a validation, and it never throws for a
    /// missing id. Order is not guaranteed; callers index by <c>CyclePeriodId</c>.</para>
    /// <para>What comes back is a projection a consumer may DISPLAY, never one it may STORE: copying a period's code
    /// or window into a consumer's own rows would go stale the moment the period is renamed.</para>
    /// </summary>
    Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(
        IReadOnlyCollection<Guid> cyclePeriodIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The periods of one planning year, ordered by sequence. This is a LISTING, not a resolution: it applies no
    /// precedence and no fallback. Filtered to one address when a scope is given; every scope when it is not, because
    /// a year view is meant to show the whole calendar rather than pick from it.
    /// </summary>
    Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(
        int year,
        string? scopeType,
        string? scopeRef,
        CancellationToken cancellationToken);
}

/// <summary>What a consumer may know about a period. A consumer stores the ID and re-reads; copying the code, the name,
/// the dates or the scope into its own rows would go stale the moment the period is renamed.</summary>
public sealed record CyclePeriodSnapshot(
    Guid CyclePeriodId,
    string CycleCode,
    string CycleName,
    int Year,
    int SequenceInYear,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string CycleStatus,
    string ScopeType,
    string? ScopeRef,
    string? CountryScope,
    Guid? LegalEntityId,
    string? BusinessUnitId)
{
    public static CyclePeriodSnapshot From(PeriodEntity p) => new(
        p.Id, p.CycleCode, p.CycleName, p.Year, p.SequenceInYear, p.StartDate, p.EndDate,
        p.CycleStatus, p.EffectiveScopeType(), p.ScopeRef(),
        p.CountryScope, p.LegalEntityId, p.BusinessUnitId);
}

/// <summary>
/// The verdict. <c>Outcome</c> is one of
/// <see cref="Diten.CrmService.Domain.Entities.CyclePeriodResolutionOutcomes"/>, and a consumer MUST branch on it:
/// <c>none</c> means there is no period (not "use the last one"), and <c>ambiguous</c> means the data is broken (not
/// "pick the first candidate").
/// <para><c>ResolvedScopeType</c> says which LEVEL answered. It is informational, never a licence: a consumer that
/// learns its business unit has no period of its own may not conclude it should create one — this seam writes nothing
/// and neither may its callers.</para>
/// </summary>
public sealed record CyclePeriodResolution(
    string Outcome,
    CyclePeriodSnapshot? Period,
    IReadOnlyList<Guid> CandidateIds,
    string? Reason,
    string? ResolvedScopeType);
