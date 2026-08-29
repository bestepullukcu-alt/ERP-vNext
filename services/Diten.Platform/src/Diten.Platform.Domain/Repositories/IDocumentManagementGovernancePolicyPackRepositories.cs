using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU31A — governance policy pack application-history repository contract. Tenant-scoped via the
// TenantRepository ExecutionFilter. Deliberately NO delete method: pack application history is append-only
// governance evidence.

public interface IDocumentGovernancePolicyPackApplicationRepository
{
    Task<DocumentGovernancePolicyPackApplication> CreateAsync(
        DocumentGovernancePolicyPackApplication application, CancellationToken ct = default);

    /// <summary>Tenant-scoped read — a cross-tenant id resolves to null (no leakage).</summary>
    Task<DocumentGovernancePolicyPackApplication?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Most recent first.</summary>
    Task<IReadOnlyList<DocumentGovernancePolicyPackApplication>> GetAllForTenantAsync(CancellationToken ct = default);

    /// <summary>The latest application of a given pack key, or null if the pack was never applied.</summary>
    Task<DocumentGovernancePolicyPackApplication?> GetLatestByPackKeyAsync(string packKey, CancellationToken ct = default);
}
