using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU06 — Document Master Register repository contract. Every method is tenant-scoped via the
// TenantRepository ExecutionFilter; no hard delete (archival is a status change).

/// <summary>MOD-0029-FU06 — tenant-scoped list filter for the Document Master Register.</summary>
public sealed record MasterRegisterListFilter(
    DocumentRegisterStatus? RegisterStatus = null,
    ControlledDocumentLifecycleStatus? LifecycleStatus = null,
    DocumentCriticality? Criticality = null,
    ControlledDocumentClass? DocumentClass = null,
    Guid? OwnerCompanyId = null);

public interface IDocumentMasterRegisterRepository
{
    Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default);
    Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Duplicate-UID guard. Only meaningful when a UID has been allocated (nullable pre-FU07).</summary>
    Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default);

    /// <summary>Duplicate-code guard. Only meaningful when a code has been allocated (nullable pre-FU07).</summary>
    Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default);

    /// <summary>Register reconciliation seam (SOP §20): find the row that projects a given controlled document.</summary>
    Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default);

    Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default);
}
