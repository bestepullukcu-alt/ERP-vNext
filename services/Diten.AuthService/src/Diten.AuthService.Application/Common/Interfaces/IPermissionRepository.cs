using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Permission?> GetByKeyAsync(string key, CancellationToken ct);

    /// <summary>
    /// FIX-CATALOG-PERM-RESYNC-DUPKEY — same as <see cref="GetByKeyAsync"/> but does NOT apply the soft-delete
    /// filter, so a re-sync can find and REACTIVATE a previously soft-deleted row (the unique-key index still owns
    /// the doc) instead of inserting a duplicate and hitting E11000.
    /// </summary>
    Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct);
    Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct);
    Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct);
    Task<Permission> CreateAsync(Permission permission, CancellationToken ct);
    Task UpdateAsync(Permission permission, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// FIX-CATALOG-PERM-REACTIVATE-PERSIST — revives a SOFT-DELETED permission by id (mirror of <see cref="DeleteAsync"/>):
    /// an Id-only update (NO soft-delete filter, unlike the filtered <see cref="UpdateAsync"/>/ReplaceOne which would
    /// match zero rows on a deleted doc). Sets IsDeleted=false, refreshes DisplayName/Description, keeps it user-defined.
    /// </summary>
    Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct);
}
