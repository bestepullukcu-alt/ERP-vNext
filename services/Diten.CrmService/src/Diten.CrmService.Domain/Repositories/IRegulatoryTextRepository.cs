using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>WP-KP-5a — store of a regulatory text kind. Tenant scoped, soft-delete aware, no delete (archive). Writes are
/// single-document and version-checked.</summary>
public interface IRegulatoryTextRepository<T> where T : RegulatoryText
{
    Task<T?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All non-deleted rows of the tenant (archived included — history stays readable).</summary>
    Task<IReadOnlyList<T>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Inserts a new version. Throws <see cref="RegulatoryTextKeyConflictException"/> when the store refuses it
    /// for the key (a concurrent writer already holds the key's open / active slot — the unique indexes).</summary>
    Task InsertAsync(T entity, CancellationToken cancellationToken);

    /// <summary>Replaces the row only while its token is <paramref name="expectedVersion"/>; the token becomes
    /// <c>expectedVersion + 1</c>. False on a concurrent write.</summary>
    Task<bool> ReplaceAsync(T entity, int expectedVersion, CancellationToken cancellationToken);
}

public interface ISafetyTextRepository : IRegulatoryTextRepository<SafetyText>;

/// <summary>WP-KP-5a-FIX-1 — the store refused a write for the key (its unique open / active index): another writer won
/// the race. The caller answers it like its own pre-check (409 open draft exists), never as a 500.</summary>
public sealed class RegulatoryTextKeyConflictException(string message, Exception? inner = null) : Exception(message, inner);

public interface ICountryLegalProfileRepository : IRegulatoryTextRepository<CountryLegalProfile>;
