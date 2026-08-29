using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029 (Faz 2a) — document-centric variant link repository. Tenant-scoped; never hard-deleted.
public interface IDocumentVariantRepository
{
    Task<DocumentVariant> CreateAsync(DocumentVariant variant, CancellationToken ct = default);
    Task<DocumentVariant?> GetByVariantRegisterEntryAsync(Guid variantRegisterEntryId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentVariant>> GetByParentRegisterEntryAsync(Guid parentRegisterEntryId, CancellationToken ct = default);
}
