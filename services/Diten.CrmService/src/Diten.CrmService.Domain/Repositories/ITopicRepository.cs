using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0162 FU02 topic taxonomy master (hierarchical, subject-scoped). Tenant scoped and soft-delete aware.
/// <b>No delete method</b>: closing a topic is the soft archive lifecycle.
/// </summary>
public interface ITopicRepository
{
    Task<Topic?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Topic>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>All non-deleted topics of one subject (used for hierarchy checks + subject-scoped listing).</summary>
    Task<IReadOnlyList<Topic>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken);

    /// <summary>The first non-deleted, non-archived topic carrying <paramref name="topicCode"/> inside
    /// <paramref name="subjectId"/> (duplicate-code guard, unique within the subject). An archived code is reusable.</summary>
    Task<Topic?> GetActiveByCodeAsync(
        Guid tenantId, Guid subjectId, string topicCode, CancellationToken cancellationToken);

    Task InsertAsync(Topic topic, CancellationToken cancellationToken);

    Task UpdateAsync(Topic topic, CancellationToken cancellationToken);
}
