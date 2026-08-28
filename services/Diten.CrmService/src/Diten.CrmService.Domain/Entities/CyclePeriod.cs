namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0165 FU06/FU07 — <b>CyclePeriod</b>: the tenant's named planning PERIOD ("2026 / cycle 3, 01.03.2026 – 30.04.2026").
/// It answers exactly one question — <i>"which period, at which address?"</i> — and nothing else.
/// <para><b>What it is not.</b> It is not a working calendar: the platform working-calendar capability says whether a DAY is a working day or a
/// holiday, while this says which PERIOD a plan belongs to, and nothing here counts working days. It is not a campaign:
/// a <see cref="Campaign"/> carries its own start/end window, which is that campaign's window and not a period. It is
/// not a frequency: how often a target is visited stays <see cref="VisitFrequencyPolicy"/> (MOD-0165 FU03), and this
/// aggregate never writes one. It is not a plan: MicroTarget rows are MOD-0155 FU05 and are never born here. It is not
/// a territory: MOD-0151 owns which country and which business units a field plan covers, and FU07 only READS that to
/// narrow a picker.</para>
/// <para><b>Scope is DISCRIMINATED, and scope is identity (FU07).</b> A period lives at exactly ONE address:
/// <see cref="ScopeType"/> names the level and exactly one of <see cref="CountryScope"/> /
/// <see cref="LegalEntityId"/> / <see cref="BusinessUnitId"/> carries the reference (none for <c>tenant</c>). A
/// combination — a country AND a legal entity AND a business unit at once — is refused, because "most specific wins"
/// needs a total order and a combination only yields a lattice. <see cref="ScopeRef"/> is the derived second half of
/// the identity key.</para>
/// <para><b>Dates are the effective window.</b> <see cref="StartDate"/> / <see cref="EndDate"/> ARE the period, so
/// there is deliberately no second EffectiveFrom/EffectiveTo pair: two date pairs would be two truths and the resolver
/// could not say which one it honours. <see cref="EndDate"/> is INCLUSIVE, and both are normalised to UTC midnight —
/// a period is a run of days, not an instant.</para>
/// <para><b>Lifecycle.</b> draft → active → closed, plus draft → closed for a plan that never ran. There is no way back:
/// closed is terminal, because MicroTarget rows, visits and reports point at a period by id and re-opening one would
/// rewrite what a past plan meant. Nothing here is ever hard-deleted, and no background job moves a period between
/// statuses — time changes no row, and the resolver reads the date window instead.</para>
/// <para>Tenant-owned (<see cref="EntityBase"/>); TenantId is server-resolved and never accepted from a payload.</para>
/// </summary>
public sealed class CyclePeriod : EntityBase
{
    /// <summary>Stable business key, unique within the tenant across every non-deleted row — <b>closed rows keep their
    /// code</b>, because a closed period's code is a permanent historical identifier and reusing it would make the
    /// provenance of an old plan ambiguous. Uniqueness is tenant-wide and deliberately NOT per scope: one code, one
    /// period, whatever address it lives at. Never renamed; rename <see cref="CycleName"/> instead.</summary>
    public string CycleCode { get; set; } = string.Empty;

    public string CycleName { get; set; } = string.Empty;

    /// <summary>The PLANNING year. It is authored, never derived from <see cref="StartDate"/>: a period that crosses a
    /// year boundary (Dec 2026 – Jan 2027) is real, and which year it counts as is a business decision rather than a
    /// calendar one.</summary>
    public int Year { get; set; }

    /// <summary>Position within <see cref="Year"/> (1, 2, 3 …). Unique per (tenant, scope, year) among non-deleted
    /// rows, closed ones included — where "scope" is the (<see cref="ScopeType"/>, <see cref="ScopeRef"/>) pair.</summary>
    public int SequenceInYear { get; set; }

    /// <summary>First day of the period, normalised to UTC midnight.</summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>Last day of the period — <b>inclusive</b> — normalised to UTC midnight. Strictly after
    /// <see cref="StartDate"/>: a zero-day period is not a plan.</summary>
    public DateTimeOffset EndDate { get; set; }

    /// <summary>
    /// FU07 — which LEVEL this period lives at: <see cref="CyclePeriodScopeTypes"/> (tenant / country / legal-entity /
    /// business-unit). <b>Immutable after creation</b>, because the scope is half of the period's identity and an
    /// identity is not edited — a period at the wrong address is closed and a new one is opened.
    /// <para>Rows written by FU06 carry no value here. They are NOT migrated: <see cref="EnsureScopeType"/> derives it
    /// on read (no business unit → tenant, a business unit → business-unit), which is exactly the FU06 scope they
    /// already had, so no row can gain or lose a collision.</para>
    /// </summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>FU07 — the reference when <see cref="ScopeType"/> is <c>country</c>: an ISO alpha-2 code from the
    /// governed MOD-0048 reference set, upper-cased. Null at every other scope type. Free strings are refused, so
    /// "TR" and "Tr" can never become two calendars.</summary>
    public string? CountryScope { get; set; }

    /// <summary>FU07 — the reference when <see cref="ScopeType"/> is <c>legal-entity</c>: an MDM legal entity, proved
    /// ACTIVE and referenceable through a fail-closed cross-service check BEFORE anything is persisted. Only the id is
    /// kept: a copied name or country would go stale the moment MDM changes it. Null at every other scope type.</summary>
    public Guid? LegalEntityId { get; set; }

    /// <summary>The reference when <see cref="ScopeType"/> is <c>business-unit</c> — a MOD-0048 published
    /// <c>business-unit</c> value code. <b>No longer an opaque string (FU07)</b>: it is validated against the same
    /// published set MOD-0151 Territory validates against, so both modules speak one vocabulary. Null at every other
    /// scope type.
    /// <para>FU06 wrote this field with <c>null</c> meaning "tenant-wide"; that reading still holds for old rows
    /// through <see cref="EnsureScopeType"/>.</para>
    /// </summary>
    public string? BusinessUnitId { get; set; }

    /// <summary>
    /// FU07 provenance stamp — <see cref="CyclePeriodBusinessUnitSources"/>: was the business unit picked from the
    /// Territory-derived candidate list, or typed in as a valid-but-out-of-plan code? It is <b>documentation, never
    /// identity</b>: uniqueness, the overlap ban and the resolver all ignore it, so two periods carrying the same
    /// business-unit code are the same scope no matter how each was authored.
    /// </summary>
    public string? BusinessUnitSource { get; set; }

    /// <summary>
    /// FU07 informational context — the country the author was filtering by when they picked the business unit.
    /// Business-unit candidates come from the territory plans covering a country, so the country is the reason a given
    /// unit was offered; keeping it lets a reader see "TR / alpha" instead of a bare code whose origin is lost.
    /// <para>It is <b>documentation, never identity</b> — the same class of field as <see cref="BusinessUnitSource"/>.
    /// Uniqueness, the overlap ban and the resolver all ignore it: two periods on the same business unit are the SAME
    /// scope whether one was filed under TR and the other under DE. Were it part of the key, one business unit could
    /// hold two colliding calendars for the same days simply because someone chose a different filter.</para>
    /// <para>Null at every scope type other than <c>business-unit</c>, and null on rows written before this field
    /// existed — a legacy period simply shows its unit without a country.</para>
    /// </summary>
    public string? BusinessUnitCountryContext { get; set; }

    public string? Description { get; set; }

    /// <summary><see cref="CyclePeriodStatuses"/> — draft / active / closed. Never set from a payload: it moves only
    /// through the activate and close endpoints.</summary>
    public string CycleStatus { get; set; } = CyclePeriodStatuses.Draft;

    public DateTimeOffset? ActivatedAt { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDraft() => string.Equals(CycleStatus, CyclePeriodStatuses.Draft, StringComparison.Ordinal);

    public bool IsActive() => string.Equals(CycleStatus, CyclePeriodStatuses.Active, StringComparison.Ordinal);

    public bool IsClosed() => string.Equals(CycleStatus, CyclePeriodStatuses.Closed, StringComparison.Ordinal);

    /// <summary>Does this period cover the given instant? Both ends are inclusive.</summary>
    public bool CoversInstant(DateTimeOffset at) => StartDate <= at && at <= EndDate;

    /// <summary>Do two periods share at least one day? Used only between ACTIVE rows of the SAME scope.</summary>
    public bool OverlapsWith(CyclePeriod other) => StartDate <= other.EndDate && other.StartDate <= EndDate;

    /// <summary>
    /// FU07 — the second half of the identity key: the reference belonging to <see cref="ScopeType"/>, normalised.
    /// <c>null</c> for the tenant scope, which is a scope of its OWN rather than "no scope".
    /// </summary>
    public string? ScopeRef() => EffectiveScopeType() switch
    {
        CyclePeriodScopeTypes.Country => Normalize(CountryScope)?.ToUpperInvariant(),
        CyclePeriodScopeTypes.LegalEntity => LegalEntityId?.ToString("D"),
        CyclePeriodScopeTypes.BusinessUnit => Normalize(BusinessUnitId),
        _ => null
    };

    /// <summary>
    /// FU07 — <see cref="ScopeType"/>, or the FU06 scope derived from <see cref="BusinessUnitId"/> when the row predates
    /// the field. Read-only and idempotent: it never writes, so a legacy row keeps behaving exactly as it did under
    /// FU06 until something else edits it.
    /// </summary>
    public string EffectiveScopeType()
        => CyclePeriodScopeTypes.IsKnown(ScopeType)
            ? CyclePeriodScopeTypes.Normalize(ScopeType)
            : Normalize(BusinessUnitId) is null
                ? CyclePeriodScopeTypes.Tenant
                : CyclePeriodScopeTypes.BusinessUnit;

    /// <summary>
    /// FU07 — stamps the derived scope onto a row loaded from storage, so every consumer downstream sees a value even
    /// for a row written by FU06. <b>This is a read-time normalisation, not a migration</b>: nothing is written back
    /// here, and no backfill script exists. The value is persisted only when the row is next written for its own
    /// reasons.
    /// </summary>
    public CyclePeriod EnsureScopeType()
    {
        ScopeType = EffectiveScopeType();
        return this;
    }

    /// <summary>
    /// FU07 invariant: exactly the reference belonging to <see cref="ScopeType"/> is present, and the other two are
    /// null (all three are null for <c>tenant</c>). A row that satisfied FU06 satisfies this too.
    /// </summary>
    public bool HasConsistentScope()
    {
        var hasCountry = Normalize(CountryScope) is not null;
        var hasLegalEntity = LegalEntityId is { } id && id != Guid.Empty;
        var hasBusinessUnit = Normalize(BusinessUnitId) is not null;

        return EffectiveScopeType() switch
        {
            CyclePeriodScopeTypes.Tenant => !hasCountry && !hasLegalEntity && !hasBusinessUnit,
            CyclePeriodScopeTypes.Country => hasCountry && !hasLegalEntity && !hasBusinessUnit,
            CyclePeriodScopeTypes.LegalEntity => hasLegalEntity && !hasCountry && !hasBusinessUnit,
            CyclePeriodScopeTypes.BusinessUnit => hasBusinessUnit && !hasCountry && !hasLegalEntity,
            _ => false
        };
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// The period lifecycle, in-domain and fail-closed (D-VOCAB = A): the runtime validates against these constants and an
/// unknown value is refused rather than quietly downgraded to <see cref="Draft"/>. Publishing a MOD-0048 set is not a
/// runtime prerequisite.
/// </summary>
public static class CyclePeriodStatuses
{
    /// <summary>Authored but not live. Draft periods may overlap freely — that is the planning space.</summary>
    public const string Draft = "draft";

    /// <summary>Live. Two active periods of the same scope may never share a day.</summary>
    public const string Active = "active";

    /// <summary>Terminal. A closed period stays readable and accepts no mutation.</summary>
    public const string Closed = "closed";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Active, Closed };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>
/// FU07 — the scope levels a period can live at, in-domain and fail-closed (D-VOCAB-SCOPE = A), for the same reason the
/// statuses are: each value changes what the resolver does, so it is STRUCTURAL and a tenant cannot extend it. An
/// unknown value is refused (400) and is never quietly read as <see cref="Tenant"/>.
/// <para>The spellings deliberately match the platform working calendar's scope types so a reader of both modules
/// learns ONE mental model — but no code is shared, because the two live in different services. CRM has no
/// <c>organization-unit</c> level: an org unit does not own a commercial planning calendar here.</para>
/// </summary>
public static class CyclePeriodScopeTypes
{
    /// <summary>The whole tenant. A scope of its OWN, not the absence of one.</summary>
    public const string Tenant = "tenant";

    /// <summary>One country, referenced by an ISO alpha-2 code from the governed reference set.</summary>
    public const string Country = "country";

    /// <summary>One MDM legal entity, referenced by id and proved referenceable before persistence.</summary>
    public const string LegalEntity = "legal-entity";

    /// <summary>One business unit, referenced by a published MOD-0048 <c>business-unit</c> value code.</summary>
    public const string BusinessUnit = "business-unit";

    /// <summary>
    /// Resolution precedence, MOST SPECIFIC FIRST. This is the single definition of the order: the resolver walks this
    /// array, and no second if/else chain restates it — an order written twice is two orders.
    /// </summary>
    public static readonly IReadOnlyList<string> ByPrecedence =
        new[] { BusinessUnit, LegalEntity, Country, Tenant };

    public static readonly IReadOnlyList<string> All = ByPrecedence;

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>
/// FU07 — where a period's business-unit code came from. A provenance stamp for the reader, never part of the identity:
/// the resolver, the uniqueness rule and the overlap ban all ignore it.
/// </summary>
public static class CyclePeriodBusinessUnitSources
{
    /// <summary>Chosen from the list derived from MOD-0151 Territory plans (country + window match).</summary>
    public const string Territory = "territory";

    /// <summary>A valid published business-unit code that no matching Territory plan covers — accepted on purpose, so a
    /// period can be planned before its field plan exists.</summary>
    public const string Manual = "manual";

    public static readonly IReadOnlyList<string> All = new[] { Territory, Manual };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);
}

/// <summary>Published ceilings for the CyclePeriod write path, so a UI needs no hardcoded limit.</summary>
public static class CyclePeriodLimits
{
    public const int MaxCycleCodeLength = 40;
    public const int MaxCycleNameLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxBusinessUnitIdLength = 60;

    /// <summary>FU07 — ISO alpha-2, so exactly two characters.</summary>
    public const int CountryScopeLength = 2;

    public const int MinYear = 2000;
    public const int MaxYear = 2100;
    public const int MinSequenceInYear = 1;
    public const int MaxSequenceInYear = 99;
}

/// <summary>How <c>resolve-active</c> answered. <c>None</c> is an ANSWER, never a default period.</summary>
public static class CyclePeriodResolutionOutcomes
{
    public const string Resolved = "resolved";
    public const string None = "none";

    /// <summary>More than one active period of the same scope covers the instant — data that violates the overlap ban.
    /// The resolver reports it instead of guessing which one wins, and does NOT fall through to a broader level:
    /// falling through would hide a data defect behind a plausible answer.</summary>
    public const string Ambiguous = "ambiguous";

    public static readonly IReadOnlyList<string> All = new[] { Resolved, None, Ambiguous };
}
