using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Domain.Repositories;

public interface IOrganizationUnitRepository
{
    Task<OrganizationUnit> CreateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default);
    Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationUnit>> GetAllAsync(CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// MOD-0288-FU02 — the reporting GRAPH, as opposed to a single unit.
///
/// <para>⚠ A SEPARATE INTERFACE, AND NOT FOR TIDINESS. Widening
/// <see cref="IOrganizationUnitRepository"/> would have forced every existing test double to grow three
/// members it has no opinion about — and a double that grows a member it does not care about grows it as a
/// stub, which is how a fake ends up quietly answering "token 0, nothing changed" for a guard that depends on
/// the answer. Only the code that walks or serializes the graph asks for this.</para>
/// </summary>
public interface IOrganizationReportingGraphRepository
{
    /// <summary>
    /// One round trip for a whole LEVEL of the reporting graph.
    ///
    /// <para>⚠ THIS EXISTS BECAUSE OF A MEASURED COST, NOT FOR TIDINESS. <c>OrganizationUnitCycleGuard</c> used
    /// to issue one <see cref="GetByIdAsync"/> per hop, so a depth-32 chain was 32 round trips — and with a
    /// second reporting line the combined graph doubles the frontier. Walking level by level with this method
    /// keeps it at one call per level however wide the level gets.</para>
    /// </summary>
    Task<IReadOnlyList<OrganizationUnit>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /*
     * ── MOD-0288-FU02 — THE GRAPH CONCURRENCY GUARD (pack §12) ────────────────────────────────────────────
     *
     * ⚠ PER-DOCUMENT CAS DOES NOT PROTECT A GRAPH. Two concurrent re-parentings can each be acyclic in
     * isolation and cyclic together: writer 1 sets A's parent to B while writer 2 sets B's parent to A, and
     * each validates against a snapshot taken before the other wrote. Nothing about either document is stale,
     * so a version check on either one passes.
     *
     * ⚠ AND THE SERVICE RUNS AS MORE THAN ONE PROCESS, so a `lock`, a semaphore or any in-memory
     * serialization is not a solution — it protects one instance while the other writes the other half of the
     * cycle. Re-reading the chain immediately before the write is not evidence either; that is the same
     * read-then-write window, only narrower.
     *
     * The guarantee therefore lives where every process meets: ONE document per tenant carrying a structure
     * token. A parent mutation reads the token BEFORE it validates, and must win a compare-and-set on it
     * before it writes. Two racing re-parentings read the same token, and exactly one CAS succeeds; the loser
     * gets 409 and re-reads a graph that now contains the winner's edge.
     *
     * ⚠ CREATE IS DELIBERATELY NOT GUARDED, AND THE PROOF IS SHORT: a brand-new unit's id is unknown to every
     * other writer, so nothing can point AT it, so no path can return to it. A create cannot close a cycle.
     * Guarding it would serialize a fifty-unit import into fifty 409s to buy nothing.
     */

    /// <summary>The current tenant's structure token. Zero when no parent mutation has ever landed.</summary>
    Task<long> ReadStructureTokenAsync(CancellationToken ct = default);

    /// <summary>
    /// Advances the token from <paramref name="expectedToken"/>. Returns <c>false</c> — never throws — when
    /// another writer got there first; that is an ordinary race outcome, and the caller answers 409.
    /// </summary>
    Task<bool> TryAdvanceStructureTokenAsync(long expectedToken, CancellationToken ct = default);
}
