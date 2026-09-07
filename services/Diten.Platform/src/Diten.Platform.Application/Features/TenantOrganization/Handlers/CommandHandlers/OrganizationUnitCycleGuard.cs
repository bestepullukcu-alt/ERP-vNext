using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

/// <summary>
/// MOD-0288-FU02 — reporting-graph admissibility for BOTH lines.
///
/// <para>Three rules, and they are not the same rule three times:</para>
/// <list type="number">
///   <item>a unit is not its own parent on either line;</item>
///   <item>neither line, walked alone, returns to the unit being edited (the pre-FU02 rule, unchanged);</item>
///   <item>the COMBINED typed graph — every functional edge and every administrative edge at once — does not
///     return to it either. A-functional-&gt;B together with B-administrative-&gt;A is invalid.</item>
/// </list>
///
/// <para>⚠ RULE 3 IS A BUSINESS RULE, NOT A CONSEQUENCE OF RULE 2, AND ITS REASON IS AMBIGUITY — NOT
/// TRAVERSAL SAFETY (§8 decision 3). Approval traverses NEITHER line: task scope walks a POSITION's
/// reports-to chain and MOD-0023 resolves the approver. So a cross-line cycle would overflow nothing; it
/// would produce a structure whose meaning nobody can state. The distinction is recorded here because a wrong
/// reason invites the wrong relaxation later — someone measures that traversal is fine and drops the rule.</para>
///
/// <para>Fail-closed is deliberate: tightening this after cyclic data exists is impossible, loosening it later
/// is cheap.</para>
///
/// <para>⚠ DEPTH 32 matches every other traversal guard in this codebase — six of them, one number. They are
/// deliberately not named here: the FU02 boundary test is a plain name check over these files (§16.4), and a
/// comment that mentions the task-scope resolver is indistinguishable to it from code that calls it. Weakening
/// the check to parse comments would weaken the guard to save the sentence.</para>
/// </summary>
internal static class OrganizationUnitCycleGuard
{
    public const int MaxDepth = 32;

    public const string CycleMessage = "Organization Unit parent cycle detected.";
    public const string DepthMessage = "Organization Unit parent chain exceeded maximum depth.";

    /// <summary>
    /// Pre-FU02 signature, kept so a caller that only moves the functional line reads the same as before.
    /// </summary>
    public static Task<Response<NoContent>> EnsureNoCycleAsync(
        IOrganizationReportingGraphRepository repository,
        Guid? currentId,
        Guid parentId,
        CancellationToken ct)
        => EnsureNoCycleAsync(repository, currentId, parentId, null, ct);

    /// <summary>
    /// Validates the graph the unit would have AFTER the proposed edges land. Either parent may be null.
    /// </summary>
    public static async Task<Response<NoContent>> EnsureNoCycleAsync(
        IOrganizationReportingGraphRepository repository,
        Guid? currentId,
        Guid? functionalParentId,
        Guid? administrativeParentId,
        CancellationToken ct)
    {
        if (!currentId.HasValue)
        {
            /*
             * A unit that does not exist yet cannot be reached from anywhere, so no edge out of it can return
             * to it. Same proof as the one that exempts create from the structure-token guard.
             */
            return Response<NoContent>.Success(204);
        }

        // Each line alone first, so the message names the line the administrator actually touched…
        if (functionalParentId.HasValue)
        {
            var perLine = await WalkAsync(repository, currentId.Value, [functionalParentId.Value], functional: true, administrative: false, ct);
            if (!perLine.IsSuccessful)
            {
                return perLine;
            }
        }

        if (administrativeParentId.HasValue)
        {
            var perLine = await WalkAsync(repository, currentId.Value, [administrativeParentId.Value], functional: false, administrative: true, ct);
            if (!perLine.IsSuccessful)
            {
                return perLine;
            }
        }

        // …then the combined graph, which catches the cross-line cycle neither pass above can see.
        var seeds = new List<Guid>(2);
        if (functionalParentId.HasValue)
        {
            seeds.Add(functionalParentId.Value);
        }

        if (administrativeParentId.HasValue)
        {
            seeds.Add(administrativeParentId.Value);
        }

        return seeds.Count == 0
            ? Response<NoContent>.Success(204)
            : await WalkAsync(repository, currentId.Value, seeds, functional: true, administrative: true, ct);
    }

    /*
     * Breadth-first, ONE LEVEL PER ROUND TRIP. The old guard read one node per hop, so depth 32 was 32 calls
     * and two lines would have been 64. Here a level is one GetByIdsAsync however wide it is, and `seen`
     * doubles as the memo that stops a diamond from being re-read.
     */
    private static async Task<Response<NoContent>> WalkAsync(
        IOrganizationReportingGraphRepository repository,
        Guid currentId,
        IReadOnlyCollection<Guid> seeds,
        bool functional,
        bool administrative,
        CancellationToken ct)
    {
        var seen = new HashSet<Guid>();
        var frontier = new List<Guid>();

        foreach (var seed in seeds)
        {
            if (seed == currentId)
            {
                return Response<NoContent>.Fail(CycleMessage, 409);
            }

            if (seen.Add(seed))
            {
                frontier.Add(seed);
            }
        }

        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (frontier.Count == 0)
            {
                return Response<NoContent>.Success(204);
            }

            var nodes = await repository.GetByIdsAsync(frontier, ct);
            var next = new List<Guid>();

            foreach (var parentId in nodes.SelectMany(node => Edges(node, functional, administrative)))
            {
                if (parentId == currentId)
                {
                    return Response<NoContent>.Fail(CycleMessage, 409);
                }

                if (seen.Add(parentId))
                {
                    next.Add(parentId);
                }
            }

            frontier = next;
        }

        /*
         * ⚠ THE FRONTIER IS STILL NON-EMPTY AFTER 32 LEVELS. Reported as depth rather than as a cycle: a chain
         * that is merely too long and one that closes are different defects, and telling an administrator
         * "cycle" about a 33-deep tree sends them looking for something that is not there.
         */
        return frontier.Count == 0
            ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail(DepthMessage, 409);
    }

    private static IEnumerable<Guid> Edges(OrganizationUnit node, bool functional, bool administrative)
    {
        if (functional && node.ParentOrganizationUnitId.HasValue)
        {
            yield return node.ParentOrganizationUnitId.Value;
        }

        if (administrative && node.AdministrativeParentOrganizationUnitId.HasValue)
        {
            yield return node.AdministrativeParentOrganizationUnitId.Value;
        }
    }
}
