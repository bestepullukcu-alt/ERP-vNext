using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU04 — generalized document access matrix policy repository contract. Every method is tenant-scoped via
// the TenantRepository ExecutionFilter; soft delete only.

public interface IDocumentAccessPolicyRepository
{
    Task<DocumentAccessPolicyEntry> CreateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default);
    Task<DocumentAccessPolicyEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentAccessPolicyEntry>> ListAsync(
        string? targetType,
        string? targetId,
        string? principalType,
        string? principalId,
        string? effect,
        string? action,
        string? status,
        CancellationToken ct = default);

    /// <summary>All active-collection policies whose (TargetType, TargetId) is in the supplied ancestor set. Used by
    /// the effective-access resolver to gather inherited + explicit rows in a single round trip.</summary>
    Task<IReadOnlyList<DocumentAccessPolicyEntry>> GetByTargetsAsync(
        IReadOnlyList<(DocumentAccessTargetType TargetType, string TargetId)> targets,
        CancellationToken ct = default);

    Task<DocumentAccessPolicyEntry?> FindDuplicateAsync(
        DocumentAccessTargetType targetType,
        string targetId,
        DocumentAccessPrincipalType principalType,
        string principalId,
        DocumentAccessEffect effect,
        CancellationToken ct = default);

    Task<bool> UpdateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}
