using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.PayrollIntegrationGovernance;

internal sealed class InMemoryPayrollIntegrationGovernanceRepository : IPayrollIntegrationGovernanceRepository
{
    private readonly Guid _tenantId;
    private readonly List<PayrollIntegrationRun> _runs = [];
    private readonly List<PayrollIntegrationSourceLink> _sourceLinks = [];
    private readonly List<PayrollIntegrationMappingControl> _mappingControls = [];
    private readonly List<PayrollReconciliationControl> _reconciliationControls = [];
    private readonly List<PayrollIntegrationException> _exceptions = [];
    private readonly List<PayrollIntegrationRetryReplayRequest> _replayRequests = [];
    private readonly List<PayrollIntegrationEvidenceExportReference> _evidenceExports = [];
    private readonly List<PayrollIntegrationHealthSnapshot> _healthSnapshots = [];

    public InMemoryPayrollIntegrationGovernanceRepository(Guid tenantId) => _tenantId = tenantId;

    public IReadOnlyList<PayrollIntegrationRun> Runs => _runs;
    public IReadOnlyList<PayrollIntegrationSourceLink> SourceLinks => _sourceLinks;
    public IReadOnlyList<PayrollIntegrationEvidenceExportReference> EvidenceExports => _evidenceExports;

    public Task<PayrollIntegrationRun> CreateRunAsync(PayrollIntegrationRun run, CancellationToken ct = default)
    {
        _runs.Add(run);
        return Task.FromResult(run);
    }

    public Task<PayrollIntegrationRun?> GetRunByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_runs.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

    public Task<IReadOnlyList<PayrollIntegrationRun>> GetRunsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PayrollIntegrationRun> result = _runs.Where(x => x.TenantId == _tenantId && !x.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> ExistsActiveRunCodeAsync(string runCode, Guid? excludeId = null, CancellationToken ct = default) =>
        Task.FromResult(_runs.Any(x => x.TenantId == _tenantId && !x.IsDeleted && x.RunCode == runCode && (!excludeId.HasValue || x.Id != excludeId.Value)));

    public Task<bool> ExistsActiveRunIdempotencyKeyAsync(string idempotencyKey, Guid? excludeId = null, CancellationToken ct = default) =>
        Task.FromResult(_runs.Any(x => x.TenantId == _tenantId && !x.IsDeleted && x.IdempotencyKey == idempotencyKey && (!excludeId.HasValue || x.Id != excludeId.Value)));

    public Task UpdateRunAsync(PayrollIntegrationRun run, CancellationToken ct = default)
    {
        run.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<bool> ArchiveRunAsync(Guid id, CancellationToken ct = default)
    {
        var run = _runs.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted);
        if (run == null)
        {
            return Task.FromResult(false);
        }

        run.IsDeleted = true;
        run.DeletedAt = DateTimeOffset.UtcNow;
        run.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<PayrollIntegrationSourceLink> CreateSourceLinkAsync(PayrollIntegrationSourceLink sourceLink, CancellationToken ct = default)
    {
        _sourceLinks.Add(sourceLink);
        return Task.FromResult(sourceLink);
    }

    public Task<IReadOnlyList<PayrollIntegrationSourceLink>> GetSourceLinksAsync(Guid runId, CancellationToken ct = default)
    {
        IReadOnlyList<PayrollIntegrationSourceLink> result = _sourceLinks.Where(x => x.TenantId == _tenantId && x.RunId == runId && !x.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<PayrollIntegrationMappingControl?> GetMappingControlByIdAsync(Guid runId, Guid controlId, CancellationToken ct = default) =>
        Task.FromResult(_mappingControls.FirstOrDefault(x => x.TenantId == _tenantId && x.RunId == runId && x.Id == controlId && !x.IsDeleted));

    public Task UpsertMappingControlAsync(PayrollIntegrationMappingControl control, CancellationToken ct = default)
    {
        if (_mappingControls.All(x => x.Id != control.Id))
        {
            _mappingControls.Add(control);
        }

        control.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PayrollIntegrationMappingControl>> GetMappingControlsAsync(Guid runId, CancellationToken ct = default)
    {
        IReadOnlyList<PayrollIntegrationMappingControl> result = _mappingControls.Where(x => x.TenantId == _tenantId && x.RunId == runId && !x.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<PayrollReconciliationControl> CreateReconciliationControlAsync(PayrollReconciliationControl control, CancellationToken ct = default)
    {
        _reconciliationControls.Add(control);
        return Task.FromResult(control);
    }

    public Task<IReadOnlyList<PayrollReconciliationControl>> GetReconciliationControlsAsync(Guid runId, CancellationToken ct = default)
    {
        IReadOnlyList<PayrollReconciliationControl> result = _reconciliationControls.Where(x => x.TenantId == _tenantId && x.RunId == runId && !x.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<PayrollIntegrationException> CreateExceptionAsync(PayrollIntegrationException exception, CancellationToken ct = default)
    {
        _exceptions.Add(exception);
        return Task.FromResult(exception);
    }

    public Task<PayrollIntegrationException?> GetExceptionByIdAsync(Guid runId, Guid exceptionId, CancellationToken ct = default) =>
        Task.FromResult(_exceptions.FirstOrDefault(x => x.TenantId == _tenantId && x.RunId == runId && x.Id == exceptionId && !x.IsDeleted));

    public Task UpdateExceptionAsync(PayrollIntegrationException exception, CancellationToken ct = default)
    {
        exception.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PayrollIntegrationException>> GetExceptionsAsync(Guid runId, CancellationToken ct = default)
    {
        IReadOnlyList<PayrollIntegrationException> result = _exceptions.Where(x => x.TenantId == _tenantId && x.RunId == runId && !x.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> ExistsActiveReplayIdempotencyKeyAsync(Guid runId, string idempotencyKey, CancellationToken ct = default) =>
        Task.FromResult(_replayRequests.Any(x => x.TenantId == _tenantId && x.RunId == runId && x.IdempotencyKey == idempotencyKey && !x.IsDeleted));

    public Task<PayrollIntegrationRetryReplayRequest> CreateRetryReplayRequestAsync(PayrollIntegrationRetryReplayRequest request, CancellationToken ct = default)
    {
        _replayRequests.Add(request);
        return Task.FromResult(request);
    }

    public Task<IReadOnlyList<PayrollIntegrationRetryReplayRequest>> GetRetryReplayRequestsAsync(Guid runId, CancellationToken ct = default)
    {
        IReadOnlyList<PayrollIntegrationRetryReplayRequest> result = _replayRequests.Where(x => x.TenantId == _tenantId && x.RunId == runId && !x.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<PayrollIntegrationEvidenceExportReference> CreateEvidenceExportReferenceAsync(PayrollIntegrationEvidenceExportReference reference, CancellationToken ct = default)
    {
        _evidenceExports.Add(reference);
        return Task.FromResult(reference);
    }

    public Task<IReadOnlyList<PayrollIntegrationEvidenceExportReference>> GetEvidenceExportReferencesAsync(Guid runId, CancellationToken ct = default)
    {
        IReadOnlyList<PayrollIntegrationEvidenceExportReference> result = _evidenceExports.Where(x => x.TenantId == _tenantId && x.RunId == runId && !x.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<PayrollIntegrationHealthSnapshot> CreateHealthSnapshotAsync(PayrollIntegrationHealthSnapshot snapshot, CancellationToken ct = default)
    {
        _healthSnapshots.Add(snapshot);
        return Task.FromResult(snapshot);
    }

    public Task<PayrollIntegrationHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid? runId, CancellationToken ct = default) =>
        Task.FromResult(_healthSnapshots.Where(x => x.TenantId == _tenantId && x.RunId == runId && !x.IsDeleted).OrderByDescending(x => x.CheckedAt).FirstOrDefault());

    public void Add(PayrollIntegrationRun run) => _runs.Add(run);
    public void Add(PayrollIntegrationException exception) => _exceptions.Add(exception);
}
