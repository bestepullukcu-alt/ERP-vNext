using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// WP-VW-W4 (W4-BE-b2) — competitor products with an EXPLICIT tenant on every call (the workflow outcome applier runs
/// without an HTTP request) and a compare-and-swap replace (the stored version must still be the expected one).
/// </summary>
public interface ICompetitorProductRepository
{
    Task<CompetitorProduct?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<IReadOnlyList<CompetitorProduct>> ListAsync(Guid tenantId, CancellationToken ct);

    Task InsertAsync(CompetitorProduct record, CancellationToken ct);

    /// <summary>True when the stored version equalled <paramref name="expectedVersion"/>; the record then carries
    /// <c>expectedVersion + 1</c>. False (version unchanged) on a lost race.</summary>
    Task<bool> ReplaceAsync(CompetitorProduct record, int expectedVersion, CancellationToken ct);
}

public interface ICompetitorProposalDecisionRepository
{
    Task<CompetitorProposalDecision?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>The latest decision of a proposal (applied or not), or null.</summary>
    Task<CompetitorProposalDecision?> GetByProposalAsync(Guid tenantId, Guid proposalId, CancellationToken ct);

    Task<IReadOnlyList<CompetitorProposalDecision>> ListByProposalsAsync(Guid tenantId, IReadOnlyCollection<Guid> proposalIds, CancellationToken ct);

    Task InsertAsync(CompetitorProposalDecision record, CancellationToken ct);

    Task<bool> ReplaceAsync(CompetitorProposalDecision record, int expectedVersion, CancellationToken ct);
}
