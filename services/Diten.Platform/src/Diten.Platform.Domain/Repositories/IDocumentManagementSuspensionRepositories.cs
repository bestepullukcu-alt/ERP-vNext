using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU13 — suspension / retirement / temporary-instruction repository contracts. Tenant-scoped; never
// hard-deleted (case + evidence history is permanent).

public interface IDocumentSuspensionCaseRepository
{
    Task<DocumentSuspensionCase> CreateAsync(DocumentSuspensionCase suspensionCase, CancellationToken ct = default);
    Task<DocumentSuspensionCase?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentSuspensionCase>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);

    /// <summary>The most recent case that is not Closed/Cancelled/Rejected, if any (used for idempotent opening).</summary>
    Task<DocumentSuspensionCase?> GetOpenAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentSuspensionCase suspensionCase, CancellationToken ct = default);
}

public interface IDocumentRetirementCaseRepository
{
    Task<DocumentRetirementCase> CreateAsync(DocumentRetirementCase retirementCase, CancellationToken ct = default);
    Task<DocumentRetirementCase?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentRetirementCase>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentRetirementCase retirementCase, CancellationToken ct = default);
}

public interface ITemporaryInstructionControlRepository
{
    Task<TemporaryInstructionControl> CreateAsync(TemporaryInstructionControl control, CancellationToken ct = default);
    Task<TemporaryInstructionControl?> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);

    /// <summary>
    /// MOD-0029-FU32 — read-only enumeration for the expiry sweep. Additive: no existing caller changes, and this
    /// method neither filters nor mutates anything the per-entry lookup would not already return.
    /// </summary>
    Task<IReadOnlyList<TemporaryInstructionControl>> GetAllForTenantAsync(CancellationToken ct = default);

    Task<bool> UpdateAsync(TemporaryInstructionControl control, CancellationToken ct = default);
}
