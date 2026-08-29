using Diten.CrmService.Domain.Entities;
using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Features.CyclePeriod.Rules;

/// <summary>
/// MOD-0165 FU06/FU07 active-period resolution, as a PURE function: "which period is in force at this instant, at the
/// most specific address the caller named?". No repository, no clock, no I/O — the handler loads the active rows and
/// this decides.
/// <para><b>Precedence, never merge (FU07).</b> The engine walks
/// <see cref="CyclePeriodScopeTypes.ByPrecedence"/> — business-unit → legal-entity → country → tenant — and answers
/// from the FIRST level that has a covering row. Two rules make that safe:</para>
/// <list type="bullet">
/// <item><description><b>An unnamed level is SKIPPED.</b> A caller that passes no legal entity never sees legal-entity
/// periods, even when they exist. This is what keeps an FU06-shaped call — an instant plus a business unit — answering
/// exactly what FU06 answered, forever, no matter how many country or legal-entity periods the tenant later
/// creates.</description></item>
/// <item><description><b>A level that answers STOPS the walk.</b> Including when it answers
/// <c>ambiguous</c>: two active rows at one address means the overlap ban was bypassed there, and quietly falling
/// through to a broader level would hide a data defect behind a plausible answer — the exact opposite of why the
/// <c>ambiguous</c> outcome exists.</description></item>
/// </list>
/// <para>The sets are never combined, so an answer always comes from exactly ONE level, and
/// <c>ResolvedScopeType</c> says which — a consumer is never left guessing whether it got its own unit's calendar or
/// the tenant's.</para>
/// <para><b>Three outcomes, because <c>none</c> and "broken" are different facts.</b> No covering period anywhere
/// returns <c>none</c> — never the nearest period, never a made-up one. This mirrors MOD-0165 FU03's resolve engine,
/// where unknown ≠ default and a tie is a conflict, so a consumer learns one mental model rather than two.</para>
/// <para><b>Time never mutates a row.</b> An active period whose window has passed simply stops resolving; no job
/// closes it. Closing is an operator decision.</para>
/// </summary>
public static class CyclePeriodResolveEngine
{
    /// <summary>The verdict, the candidates that produced it, and the level it came from.</summary>
    public sealed record Resolution(
        string Outcome,
        PeriodEntity? Period,
        IReadOnlyList<Guid> CandidateIds,
        string? Reason,
        string? ResolvedScopeType);

    /// <summary>The address a caller named, level by level. A <c>null</c> means "I am not asking at that level".</summary>
    public sealed record ScopeRequest(string? Country, Guid? LegalEntityId, string? BusinessUnitId)
    {
        public static readonly ScopeRequest TenantOnly = new(null, null, null);
    }

    /// <param name="activePeriods">ACTIVE, non-deleted rows of the tenant. Any other status is ignored on purpose.</param>
    /// <param name="at">The instant to resolve. Both window ends are inclusive.</param>
    /// <param name="scope">Which levels the caller is asking at. Unnamed levels are skipped; tenant is always tried last.</param>
    public static Resolution Resolve(
        IEnumerable<PeriodEntity> activePeriods, DateTimeOffset at, ScopeRequest scope)
    {
        var covering = activePeriods
            .Where(p => p.IsActive() && p.CoversInstant(at))
            .ToList();

        foreach (var scopeType in CyclePeriodScopeTypes.ByPrecedence)
        {
            var scopeRef = CyclePeriodScopeRules.NormalizeScopeRefFor(
                scopeType, scope.Country, scope.LegalEntityId, scope.BusinessUnitId);

            // An unnamed level is not "no reference at that level" — it is a level the caller did not ask about.
            if (scopeType != CyclePeriodScopeTypes.Tenant && scopeRef is null)
            {
                continue;
            }

            var scoped = CyclePeriodOverlapRules.InScope(covering, scopeType, scopeRef);
            if (scoped.Count == 0)
            {
                continue;
            }

            if (scoped.Count == 1)
            {
                return new Resolution(
                    CyclePeriodResolutionOutcomes.Resolved, scoped[0], new[] { scoped[0].Id }, null, scopeType);
            }

            // Stops here on purpose: a broken level must be reported, not stepped over.
            return new Resolution(
                CyclePeriodResolutionOutcomes.Ambiguous,
                null,
                scoped.OrderBy(p => p.StartDate).Select(p => p.Id).ToList(),
                "More than one active cycle period covers the requested instant at scope "
                + CyclePeriodScopeRules.Describe(scopeType, scopeRef)
                + ". The active-overlap rule was violated; no period is selected and no broader scope is consulted.",
                scopeType);
        }

        return new Resolution(
            CyclePeriodResolutionOutcomes.None,
            null,
            Array.Empty<Guid>(),
            "No active cycle period covers the requested instant at any scope the caller named.",
            null);
    }
}
