using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;

namespace Diten.Platform.Domain.Repositories;

public interface IPayrollIntegrationGovernanceRepository
{
    Task<PayrollIntegrationRun> CreateRunAsync(PayrollIntegrationRun run, CancellationToken ct = default);
    Task<PayrollIntegrationRun?> GetRunByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollIntegrationRun>> GetRunsAsync(CancellationToken ct = default);
    Task<bool> ExistsActiveRunCodeAsync(string runCode, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> ExistsActiveRunIdempotencyKeyAsync(string idempotencyKey, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateRunAsync(PayrollIntegrationRun run, CancellationToken ct = default);
    Task<bool> ArchiveRunAsync(Guid id, CancellationToken ct = default);
    Task<PayrollIntegrationSourceLink> CreateSourceLinkAsync(PayrollIntegrationSourceLink sourceLink, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollIntegrationSourceLink>> GetSourceLinksAsync(Guid runId, CancellationToken ct = default);
    Task<PayrollIntegrationMappingControl?> GetMappingControlByIdAsync(Guid runId, Guid controlId, CancellationToken ct = default);
    Task UpsertMappingControlAsync(PayrollIntegrationMappingControl control, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollIntegrationMappingControl>> GetMappingControlsAsync(Guid runId, CancellationToken ct = default);
    Task<PayrollReconciliationControl> CreateReconciliationControlAsync(PayrollReconciliationControl control, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollReconciliationControl>> GetReconciliationControlsAsync(Guid runId, CancellationToken ct = default);
    Task<PayrollIntegrationException> CreateExceptionAsync(PayrollIntegrationException exception, CancellationToken ct = default);
    Task<PayrollIntegrationException?> GetExceptionByIdAsync(Guid runId, Guid exceptionId, CancellationToken ct = default);
    Task UpdateExceptionAsync(PayrollIntegrationException exception, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollIntegrationException>> GetExceptionsAsync(Guid runId, CancellationToken ct = default);
    Task<bool> ExistsActiveReplayIdempotencyKeyAsync(Guid runId, string idempotencyKey, CancellationToken ct = default);
    Task<PayrollIntegrationRetryReplayRequest> CreateRetryReplayRequestAsync(PayrollIntegrationRetryReplayRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollIntegrationRetryReplayRequest>> GetRetryReplayRequestsAsync(Guid runId, CancellationToken ct = default);
    Task<PayrollIntegrationEvidenceExportReference> CreateEvidenceExportReferenceAsync(PayrollIntegrationEvidenceExportReference reference, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollIntegrationEvidenceExportReference>> GetEvidenceExportReferencesAsync(Guid runId, CancellationToken ct = default);
    Task<PayrollIntegrationHealthSnapshot> CreateHealthSnapshotAsync(PayrollIntegrationHealthSnapshot snapshot, CancellationToken ct = default);
    Task<PayrollIntegrationHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid? runId, CancellationToken ct = default);
}
