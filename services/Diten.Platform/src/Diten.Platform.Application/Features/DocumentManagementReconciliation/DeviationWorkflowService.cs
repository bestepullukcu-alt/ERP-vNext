using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementReconciliation;

/// <summary>
/// MOD-0028-FU09 — deviation listing + resolve/accept workflow. Resolve/accept change status only; a deviation is
/// never hard-deleted, so the qualification trail is preserved.
/// </summary>
public sealed class DeviationWorkflowService
{
    private readonly IDocumentCollectionDeviationRepository _deviations;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DeviationWorkflowService(
        IDocumentCollectionDeviationRepository deviations, ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _deviations = deviations;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<IReadOnlyList<DeviationModel>>> ListByBaselineAsync(Guid baselineReleaseId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _deviations.GetByBaselineAsync(baselineReleaseId, ct);
        var models = rows
            .OrderByDescending(d => d.Severity)
            .ThenByDescending(d => d.DetectedAt)
            .Select(ReconciliationMapping.ToModel)
            .ToList();
        return Response<IReadOnlyList<DeviationModel>>.Success(models, 200, correlationId);
    }

    public Task<Response<DeviationModel>> ResolveAsync(Guid id, string? comment, string correlationId, CancellationToken ct) =>
        TransitionAsync(id, DeviationStatus.Resolved, comment, correlationId, ct);

    public Task<Response<DeviationModel>> AcceptAsync(Guid id, string? comment, string correlationId, CancellationToken ct) =>
        TransitionAsync(id, DeviationStatus.Accepted, comment, correlationId, ct);

    private async Task<Response<DeviationModel>> TransitionAsync(
        Guid id, DeviationStatus target, string? comment, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var deviation = await _deviations.GetByIdAsync(id, ct);
        if (deviation is null)
        {
            return Response<DeviationModel>.Fail(
                "Deviation not found.", 404, ReconciliationReasonCodes.NotFoundNonLeakage, correlationId);
        }

        deviation.Status = target;
        deviation.ResolutionComment = string.IsNullOrWhiteSpace(comment) ? deviation.ResolutionComment : comment.Trim();
        deviation.ResolvedAt = DateTimeOffset.UtcNow;
        deviation.ResolvedBy = _currentUser.ActorName;
        deviation.CorrelationId = correlationId;
        deviation.UpdatedBy = _currentUser.ActorName;
        await _deviations.UpdateAsync(deviation, ct);

        return Response<DeviationModel>.Success(ReconciliationMapping.ToModel(deviation), 200, correlationId);
    }
}

/// <summary>
/// MOD-0028-FU09 — read-only qualification readiness snapshot: whether every provisioned node has evidence, has IT +
/// QA sign-off, and there are no open blocking (Major/Critical) deviations. Advisory only in this FU — it is NOT wired
/// into MarkEffective, so the FU08 lifecycle is unchanged (a hard gate is a later task).
/// </summary>
public sealed class BaselineQualificationReadinessService
{
    private readonly IBaselineReleaseRepository _baselines;
    private readonly ICollectionInstanceRepository _instances;
    private readonly IProvisioningEvidenceRepository _evidence;
    private readonly IDocumentCollectionDeviationRepository _deviations;
    private readonly ITenantContext _tenantContext;

    public BaselineQualificationReadinessService(
        IBaselineReleaseRepository baselines,
        ICollectionInstanceRepository instances,
        IProvisioningEvidenceRepository evidence,
        IDocumentCollectionDeviationRepository deviations,
        ITenantContext tenantContext)
    {
        _baselines = baselines;
        _instances = instances;
        _evidence = evidence;
        _deviations = deviations;
        _tenantContext = tenantContext;
    }

    public async Task<Response<QualificationReadinessModel>> EvaluateAsync(Guid baselineReleaseId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var baseline = await _baselines.GetByIdAsync(baselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<QualificationReadinessModel>.Fail(
                "Baseline not found.", 404, ReconciliationReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var instances = (await _instances.GetAllForTenantAsync(ct))
            .Where(i => i.BaselineReleaseId == baselineReleaseId && i.InstanceStatus == CollectionInstanceStatus.Active)
            .ToList();
        var evidence = await _evidence.GetByBaselineAsync(baselineReleaseId, ct);
        var evidenceByInstance = evidence
            .GroupBy(e => e.CollectionInstanceId)
            .ToDictionary(g => g.Key, g => g.First());
        var deviations = await _deviations.GetByBaselineAsync(baselineReleaseId, ct);

        var missingEvidence = instances.Count(i => !evidenceByInstance.ContainsKey(i.Id));
        var permissionsApplied = evidence.Count(e => e.PermissionsApplied);
        var qaVerified = evidence.Count(e => e.QaVerified);
        var openBlocking = deviations.Count(d =>
            d.Status == DeviationStatus.Open && d.Severity is DeviationSeverity.Major or DeviationSeverity.Critical);

        var reasons = new List<string>();
        if (instances.Count == 0) reasons.Add("No provisioned instances exist for this baseline.");
        if (missingEvidence > 0) reasons.Add($"{missingEvidence} provisioned node(s) have no provisioning evidence.");
        if (openBlocking > 0) reasons.Add($"{openBlocking} open blocking deviation(s) must be resolved or accepted.");
        if (evidence.Any(e => !e.PermissionsApplied)) reasons.Add("Some evidence rows are missing IT permissions sign-off.");
        if (evidence.Any(e => !e.QaVerified)) reasons.Add("Some evidence rows are missing QA verification sign-off.");

        var ready = instances.Count > 0
            && missingEvidence == 0
            && openBlocking == 0
            && evidence.All(e => e.PermissionsApplied)
            && evidence.All(e => e.QaVerified);

        var model = new QualificationReadinessModel(
            baselineReleaseId,
            baseline.Status.ToString().ToUpperInvariant(),
            ready,
            instances.Count,
            evidence.Count,
            missingEvidence,
            permissionsApplied,
            qaVerified,
            openBlocking,
            reasons);

        return Response<QualificationReadinessModel>.Success(model, 200, correlationId);
    }
}
