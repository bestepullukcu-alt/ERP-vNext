using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU17 — controlled copy / withdrawal plan / obsolete finding repository contracts. Tenant-scoped; never
// hard-deleted.

public interface IDocumentControlledCopyRepository
{
    Task<DocumentControlledCopy> CreateAsync(DocumentControlledCopy copy, CancellationToken ct = default);
    Task<DocumentControlledCopy?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentControlledCopy>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<DocumentControlledCopy?> GetByCopyNumberAsync(Guid registerEntryId, int copyNumber, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentControlledCopy copy, CancellationToken ct = default);
}

public interface IDocumentCopyWithdrawalPlanRepository
{
    Task<DocumentCopyWithdrawalPlan> CreateAsync(DocumentCopyWithdrawalPlan plan, CancellationToken ct = default);
    Task<DocumentCopyWithdrawalPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentCopyWithdrawalPlan>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);

    /// <summary>The most recent plan that is not Completed/Cancelled, if any (used for idempotent generation).</summary>
    Task<DocumentCopyWithdrawalPlan?> GetOpenAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentCopyWithdrawalPlan plan, CancellationToken ct = default);
}

public interface IDocumentObsoleteCopyFindingRepository
{
    Task<DocumentObsoleteCopyFinding> CreateAsync(DocumentObsoleteCopyFinding finding, CancellationToken ct = default);
    Task<DocumentObsoleteCopyFinding?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentObsoleteCopyFinding>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentObsoleteCopyFinding finding, CancellationToken ct = default);
}
