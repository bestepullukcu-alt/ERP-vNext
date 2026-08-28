using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Features.CyclePeriod.Rules;

/// <summary>
/// MOD-0165 FU06/FU07 set rules, as PURE functions over rows the handler already loaded: scope matching, code
/// uniqueness, sequence uniqueness and the active-overlap ban. No repository, no clock, no I/O — so every rule can be
/// tested directly and none of them can be enforced twice in two slightly different ways.
/// <para><b>Scope (FU07).</b> A period's scope is the pair (<c>ScopeType</c>, <c>ScopeRef</c>) — tenant / country /
/// legal-entity / business-unit — and <c>tenant</c> is a scope of its OWN rather than the absence of one. FU06 rows
/// carry no ScopeType; <c>EffectiveScopeType()</c> derives it on read, and the derivation maps FU06's two cases onto
/// exactly two of FU07's four, so no legacy row changes scope.</para>
/// <para><b>The overlap ban applies to ACTIVE rows of the SAME scope only.</b> Draft rows may overlap freely — a
/// planner has to be able to sketch alternatives and lay out a whole year — and closed rows never block anything.
/// <b>Rows at DIFFERENT levels may overlap, and must be allowed to</b>: a country calendar and a business unit's own
/// calendar covering the same days is precisely the situation precedence exists to decide. Banning that would make
/// <see cref="CyclePeriodResolveEngine"/>'s fallback unreachable. What the ban buys is the only guarantee consumers
/// actually need: at any instant, at most ONE active period PER SCOPE. Note there is no "only one active row" rule:
/// that would make planning a year ahead impossible while buying nothing.</para>
/// </summary>
public static class CyclePeriodOverlapRules
{
    /// <summary>Same scope reference? <c>null</c> matches only <c>null</c>; codes compare case-insensitively.</summary>
    public static bool SameScopeRef(string? left, string? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        return left is not null && right is not null
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// FU06 compatibility shim: two business-unit-shaped values, where <c>null</c> means tenant-wide. Kept because the
    /// meaning is identical under FU07's derivation, and renaming it at every call site would churn code that is right.
    /// </summary>
    public static bool SameScope(string? left, string? right) => SameScopeRef(left, right);

    /// <summary>Does a row sit at exactly this (type, ref) address?</summary>
    public static bool IsAtScope(PeriodEntity row, string scopeType, string? scopeRef)
        => string.Equals(row.EffectiveScopeType(), scopeType, StringComparison.Ordinal)
           && SameScopeRef(row.ScopeRef(), scopeRef);

    /// <summary>Rows at one address. Used by the uniqueness checks, the overlap check and the resolver — one narrowing,
    /// so the scope rule lives in exactly one place.</summary>
    public static IReadOnlyList<PeriodEntity> InScope(
        IEnumerable<PeriodEntity> rows, string scopeType, string? scopeRef)
        => rows.Where(r => IsAtScope(r, scopeType, scopeRef)).ToList();

    /// <summary>Is this code already taken in the tenant? Closed rows STILL hold their code (a historical identifier is
    /// not recyclable), and the check is tenant-wide rather than per scope: one code names one period.</summary>
    public static bool IsCodeTaken(IEnumerable<PeriodEntity> rowsWithCode, Guid? excludeId = null)
        => rowsWithCode.Any(r => excludeId is null || r.Id != excludeId);

    /// <summary>Is (year, sequence) already taken within this scope? Closed rows count, for the same reason.</summary>
    public static bool IsSequenceTaken(
        IEnumerable<PeriodEntity> rowsOfYear,
        string scopeType,
        string? scopeRef,
        int sequenceInYear,
        Guid? excludeId = null)
        => InScope(rowsOfYear, scopeType, scopeRef)
            .Any(r => r.SequenceInYear == sequenceInYear && (excludeId is null || r.Id != excludeId));

    /// <summary>
    /// The active periods of this scope that would share a day with the given window. Empty means the window is free.
    /// The caller passes ACTIVE rows only; the excluded id is the row being activated or edited. Rows at other levels
    /// are not considered — that is the point of the rule, not an omission.
    /// </summary>
    public static IReadOnlyList<PeriodEntity> FindActiveOverlaps(
        IEnumerable<PeriodEntity> activeRows,
        string scopeType,
        string? scopeRef,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        Guid? excludeId = null)
        => InScope(activeRows, scopeType, scopeRef)
            .Where(r => (excludeId is null || r.Id != excludeId) && r.StartDate <= endDate && startDate <= r.EndDate)
            .OrderBy(r => r.StartDate)
            .ToList();

    /// <summary>A refusal a human can act on: which period blocks this one, over which days, and at which address.
    /// Without the code, the window and the scope the author cannot find the offending row.</summary>
    public static string DescribeOverlap(IEnumerable<PeriodEntity> overlaps)
        => string.Join(
            ", ",
            overlaps.Select(o =>
                $"{o.CycleCode} ({o.StartDate:yyyy-MM-dd} – {o.EndDate:yyyy-MM-dd}, "
                + $"{CyclePeriodScopeRules.Describe(o.EffectiveScopeType(), o.ScopeRef())})"));
}
