using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU10 — release gate evaluation / result / manual evidence repository contracts. Tenant-scoped; never
// hard-deleted (history is preserved).

public interface IDocumentReleaseGateEvaluationRepository
{
    Task<DocumentReleaseGateEvaluation> CreateAsync(DocumentReleaseGateEvaluation evaluation, CancellationToken ct = default);
    Task<DocumentReleaseGateEvaluation?> GetLatestAsync(Guid registerEntryId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentReleaseGateEvaluation>> GetHistoryAsync(Guid registerEntryId, CancellationToken ct = default);
}

public interface IDocumentReleaseGateResultRepository
{
    Task<DocumentReleaseGateResult> CreateAsync(DocumentReleaseGateResult result, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentReleaseGateResult>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct = default);
}

public interface IDocumentReleaseGateEvidenceRepository
{
    Task<DocumentReleaseGateEvidence> CreateAsync(DocumentReleaseGateEvidence evidence, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentReleaseGateEvidence>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);

    /// <summary>Latest valid manual evidence for a specific gate (used by the evaluator to compute manual gates).</summary>
    Task<DocumentReleaseGateEvidence?> GetLatestForGateAsync(Guid registerEntryId, ReleaseGateKey gateKey, CancellationToken ct = default);
}
