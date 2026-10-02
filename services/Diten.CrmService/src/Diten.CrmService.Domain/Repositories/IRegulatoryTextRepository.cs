using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>WP-KP-5a — store of a regulatory text kind. Tenant scoped, soft-delete aware, no delete (archive). Writes are
/// single-document and version-checked.</summary>
public interface IRegulatoryTextRepository<T> where T : RegulatoryText
{
    Task<T?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All non-deleted rows of the tenant (archived included — history stays readable).</summary>
    Task<IReadOnlyList<T>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task InsertAsync(T entity, CancellationToken cancellationToken);

    /// <summary>Replaces the row only while its token is <paramref name="expectedVersion"/>; the token becomes
    /// <c>expectedVersion + 1</c>. False on a concurrent write.</summary>
    Task<bool> ReplaceAsync(T entity, int expectedVersion, CancellationToken cancellationToken);
}

public interface ISafetyTextRepository : IRegulatoryTextRepository<SafetyText>;

public interface ICountryLegalProfileRepository : IRegulatoryTextRepository<CountryLegalProfile>;
