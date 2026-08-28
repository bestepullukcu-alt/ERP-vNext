using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU16 — repository assessment + finding repository contracts. Tenant-scoped; never hard-deleted.

public interface IDocumentRepositoryAssessmentRepository
{
    Task<DocumentRepositoryAssessment> CreateAsync(DocumentRepositoryAssessment assessment, CancellationToken ct = default);
    Task<DocumentRepositoryAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentRepositoryAssessment>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentRepositoryAssessment assessment, CancellationToken ct = default);
}

public interface IDocumentRepositoryAssessmentFindingRepository
{
    Task<DocumentRepositoryAssessmentFinding> CreateAsync(DocumentRepositoryAssessmentFinding finding, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentRepositoryAssessmentFinding>> GetByAssessmentAsync(Guid repositoryAssessmentId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentRepositoryAssessmentFinding finding, CancellationToken ct = default);
}
