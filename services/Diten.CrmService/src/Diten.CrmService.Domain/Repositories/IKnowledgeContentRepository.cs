using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0162 FU02 knowledge content master. Tenant scoped and soft-delete aware. There is deliberately <b>no delete
/// method</b>: closing content is the soft archive lifecycle, so content history stays readable.
/// </summary>
public interface IKnowledgeContentRepository
{
    Task<KnowledgeContent?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All non-deleted content of a tenant (any status, archived included — history must stay readable).</summary>
    Task<IReadOnlyList<KnowledgeContent>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The first non-deleted, non-archived content carrying <paramref name="contentCode"/> (duplicate-code
    /// guard). An archived code is reusable.</summary>
    Task<KnowledgeContent?> GetActiveByCodeAsync(Guid tenantId, string contentCode, CancellationToken cancellationToken);

    Task InsertAsync(KnowledgeContent content, CancellationToken cancellationToken);

    Task UpdateAsync(KnowledgeContent content, CancellationToken cancellationToken);
}
