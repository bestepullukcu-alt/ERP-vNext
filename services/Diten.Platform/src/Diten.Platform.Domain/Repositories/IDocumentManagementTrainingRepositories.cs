using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU11 — document training matrix requirement + assignment repository contracts. Tenant-scoped; never
// hard-deleted.

public interface IDocumentTrainingMatrixRequirementRepository
{
    Task<DocumentTrainingMatrixRequirement> CreateAsync(DocumentTrainingMatrixRequirement requirement, CancellationToken ct = default);
    Task<DocumentTrainingMatrixRequirement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentTrainingMatrixRequirement>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentTrainingMatrixRequirement requirement, CancellationToken ct = default);
}

public interface IDocumentTrainingAssignmentRepository
{
    Task<DocumentTrainingAssignment> CreateAsync(DocumentTrainingAssignment assignment, CancellationToken ct = default);
    Task<DocumentTrainingAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentTrainingAssignment>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentTrainingAssignment>> GetByRequirementAsync(Guid requirementId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentTrainingAssignment assignment, CancellationToken ct = default);
}
