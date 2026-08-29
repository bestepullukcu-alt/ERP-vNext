using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0162 FU02 subject taxonomy master. Tenant scoped and soft-delete aware. <b>No delete method</b>: closing a
/// subject is the soft archive lifecycle, so classification history stays readable.
/// </summary>
public interface ISubjectRepository
{
    Task<Subject?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Subject>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The first non-deleted, non-archived subject carrying <paramref name="subjectCode"/> (duplicate-code
    /// guard). An archived code is reusable.</summary>
    Task<Subject?> GetActiveByCodeAsync(Guid tenantId, string subjectCode, CancellationToken cancellationToken);

    Task InsertAsync(Subject subject, CancellationToken cancellationToken);

    Task UpdateAsync(Subject subject, CancellationToken cancellationToken);
}
