using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;

/// <summary>
/// MOD-0028-FU08 — DRAFT → APPROVED. Freezes the immutable snapshot manifest at review time (same deterministic
/// hash as the legacy publish path) but leaves the baseline non-instantiable until <c>MarkEffective</c>.
/// </summary>
public sealed class ApproveQmsBaselineHandler
    : IRequestHandler<ApproveQmsBaselineCommand, Response<QmsBaselineSummaryModel>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly IBaselineSnapshotManifestRepository _manifestRepository;
    private readonly BaselineSnapshotHasher _hasher;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public ApproveQmsBaselineHandler(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        IBaselineSnapshotManifestRepository manifestRepository,
        BaselineSnapshotHasher hasher,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _manifestRepository = manifestRepository;
        _hasher = hasher;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<QmsBaselineSummaryModel>> Handle(ApproveQmsBaselineCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        var baseline = await _baselineRepository.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Baseline not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        if (baseline.Status != BaselineReleaseStatus.Draft)
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Only a DRAFT baseline can be approved.", 400, QmsBaselineReasonCodes.ValidationFailed, request.CorrelationId);
        }

        if (request.ExpectedVersion > 0 && baseline.Version != request.ExpectedVersion)
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Stale baseline version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        var definitions = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
        if (definitions.Count == 0)
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Baseline has no definitions to approve.", 400, QmsBaselineReasonCodes.ValidationFailed, request.CorrelationId);
        }

        // Freeze the immutable snapshot at approval (idempotent shape with the legacy publish manifest).
        var computation = _hasher.Compute(definitions);
        var manifest = new BaselineSnapshotManifest
        {
            TenantId = tenantId,
            ManifestId = $"MAN-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            BaselineReleaseId = baseline.Id,
            ManifestVersion = "1.0",
            DefinitionIds = computation.DefinitionIds,
            DefinitionHashes = computation.DefinitionHashes,
            StructuralControlsHash = computation.StructuralControlsHash,
            SnapshotHash = computation.SnapshotHash
        };
        await _manifestRepository.CreateAsync(manifest, ct);

        var expectedVersion = baseline.Version;
        baseline.Status = BaselineReleaseStatus.Approved;
        baseline.SnapshotHash = computation.SnapshotHash;
        baseline.ManifestId = manifest.Id;
        baseline.ApprovedAt = DateTimeOffset.UtcNow;
        baseline.ApprovedBy = _currentUser.ActorName;
        baseline.ApprovalReference = string.IsNullOrWhiteSpace(request.ApprovalReference) ? null : request.ApprovalReference.Trim();
        baseline.ApprovalComment = string.IsNullOrWhiteSpace(request.ApprovalComment) ? null : request.ApprovalComment.Trim();

        var updated = await _baselineRepository.UpdateAsync(baseline, expectedVersion, ct);
        if (!updated)
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Stale baseline version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        return Response<QmsBaselineSummaryModel>.Success(
            QmsBaselineMapping.ToSummaryModel(baseline, definitions.Count), 200, request.CorrelationId);
    }
}
