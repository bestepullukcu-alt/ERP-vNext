using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU08 — controlled document lifecycle transition record contract. Tenant-scoped; never hard-deleted.

public interface IDocumentLifecycleTransitionRecordRepository
{
    Task<DocumentLifecycleTransitionRecord> CreateAsync(DocumentLifecycleTransitionRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetAllForTenantAsync(CancellationToken ct = default);
}
