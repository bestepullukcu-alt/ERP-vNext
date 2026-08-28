using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU09 — approval requirement + immutable evidence repository contracts. Tenant-scoped; never hard-deleted.

public interface IDocumentApprovalRequirementRepository
{
    Task<DocumentApprovalRequirement> CreateAsync(DocumentApprovalRequirement requirement, CancellationToken ct = default);
    Task<DocumentApprovalRequirement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentApprovalRequirement>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentApprovalRequirement requirement, CancellationToken ct = default);
}

public interface IDocumentApprovalEvidenceRepository
{
    Task<DocumentApprovalEvidence> CreateAsync(DocumentApprovalEvidence evidence, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentApprovalEvidence>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentApprovalEvidence>> GetByRequirementAsync(Guid requirementId, CancellationToken ct = default);
}
