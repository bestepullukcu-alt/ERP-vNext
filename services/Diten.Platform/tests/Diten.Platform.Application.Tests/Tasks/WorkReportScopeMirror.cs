using Diten.Platform.Application.Features.Tasks.Services;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// AN IN-MEMORY MIRROR of the scope rule, for tests only. It is NOT what production enforces.
///
/// <para><b>⚠ WHY IT LIVES HERE, MEASURED 2026-09-04.</b> This method used to sit in
/// <c>WorkReportTally</c> — in the production assembly — with ZERO production callers: the only mentions in
/// <c>src</c> were its own declaration and one doc-comment. Every one of its 15 call sites was a test. So the
/// shipped binary carried a second implementation of "whose rows may this reader see", and it was the one the
/// tests were exercising while production used another.</para>
///
/// <para>That is the "two places to disagree about the same truth" shape this codebase names elsewhere, in its
/// most dangerous form: the tested copy and the enforcing copy were different pieces of code. CONTROL TOWER
/// proved the cost — editing the repository to drop the scope whenever a filter was present left the whole
/// suite green, because nothing was testing the repository's composition at all.</para>
///
/// <para><b>Why not give it a production caller instead.</b> There is no honest one. The report filters in the
/// DATABASE by design — that is the whole reason Faz 5a refused <c>GetAllForTenantAsync</c> — so an in-memory
/// scope pass would be either redundant (Mongo already applied the terms) or a full-collection scan. Option (a)
/// had no real form, so the code moved to where its only users are.</para>
///
/// <para><b>What enforces the rule now, and what this is for.</b> Production admits rows through
/// <c>WorkReportRepository.BuildMatchFilter</c>, whose rendered query is asserted by
/// <c>WorkReportQueryCompositionTests</c>. This mirror stays as a READABLE SPECIFICATION of the same rule —
/// useful for stating scope semantics in a sentence a person can check — but it proves nothing about the
/// shipped query, and no test may treat it as if it did.</para>
/// </summary>
internal static class WorkReportScopeMirror
{
    /// <summary>
    /// Whether a row is inside a scope — the same question the Mongo filter asks, in the form a test can ask it.
    /// </summary>
    public static bool InScope(WorkReportScope scope, WorkReportRow row)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(row);

        if (scope.TenantWide)
        {
            return true;
        }

        /*
         * Fail-closed: an empty scope matches nothing. An OR over zero branches is TRUE in every query language
         * there is — and the rendered production query really does build the scope as an `$or`, which is why
         * `WorkReportRepository.ScopeFilter` keeps an impossible fallback clause for the same case.
         */
        if (scope.MatchesNothing)
        {
            return false;
        }

        return scope.OrganizationUnitIds.Contains(row.OrganizationUnitId)
            || (row.PoolPositionId is { } pool && scope.PositionIds.Contains(pool))
            || (row.AssigneeUserId is { } assignee && scope.UserIds.Contains(assignee))
            || (row.CreatedByUserId is { } requester && scope.UserIds.Contains(requester));
    }
}
