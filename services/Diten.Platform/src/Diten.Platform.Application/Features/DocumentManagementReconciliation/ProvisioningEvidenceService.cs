using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementReconciliation;

/// <summary>
/// MOD-0028-FU09 — provisioning evidence + IT/QA sign-off. Idempotent upsert keyed by CollectionInstanceId (one
/// evidence row per node); sign-offs update fields non-destructively with audit. Sidecar only — never touches the
/// MOD-0028 definition/instance identity.
/// </summary>
public sealed class ProvisioningEvidenceService
{
    private readonly IProvisioningEvidenceRepository _evidence;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public ProvisioningEvidenceService(
        IProvisioningEvidenceRepository evidence, ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _evidence = evidence;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<ProvisioningEvidenceModel>> UpsertAsync(EvidenceUpsertInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        if (input.BaselineReleaseId == Guid.Empty || input.CollectionInstanceId == Guid.Empty || string.IsNullOrWhiteSpace(input.FullPath))
        {
            return Response<ProvisioningEvidenceModel>.Fail(
                "BaselineReleaseId, CollectionInstanceId and FullPath are required.", 400,
                ReconciliationReasonCodes.ValidationFailed, correlationId);
        }

        var existing = await _evidence.GetByCollectionInstanceAsync(input.CollectionInstanceId, ct);
        if (existing is not null)
        {
            existing.BaselineReleaseId = input.BaselineReleaseId;
            existing.CollectionDefinitionId = input.CollectionDefinitionId ?? existing.CollectionDefinitionId;
            existing.RegisterFolderId = input.RegisterFolderId ?? existing.RegisterFolderId;
            existing.RegisterParentFolderId = input.RegisterParentFolderId ?? existing.RegisterParentFolderId;
            existing.FullPath = input.FullPath;
            existing.PlatformProvider = input.PlatformProvider;
            existing.PlatformFolderId = input.PlatformFolderId ?? existing.PlatformFolderId;
            existing.PlatformParentId = input.PlatformParentId ?? existing.PlatformParentId;
            existing.ProvisioningStatus = input.ProvisioningStatus ?? existing.ProvisioningStatus;
            existing.CreatedOnPlatformAt = input.CreatedOnPlatformAt ?? existing.CreatedOnPlatformAt;
            existing.CreatedOnPlatformBy = input.CreatedOnPlatformBy ?? existing.CreatedOnPlatformBy;
            existing.DeviationComment = input.DeviationComment ?? existing.DeviationComment;
            existing.CorrelationId = correlationId;
            existing.UpdatedBy = _currentUser.ActorName;
            await _evidence.UpdateAsync(existing, ct);
            return Response<ProvisioningEvidenceModel>.Success(ReconciliationMapping.ToModel(existing), 200, correlationId);
        }

        var created = await _evidence.CreateAsync(new DocumentCollectionProvisioningEvidence
        {
            TenantId = _tenantContext.TenantId,
            BaselineReleaseId = input.BaselineReleaseId,
            CollectionDefinitionId = input.CollectionDefinitionId,
            CollectionInstanceId = input.CollectionInstanceId,
            RegisterFolderId = input.RegisterFolderId,
            RegisterParentFolderId = input.RegisterParentFolderId,
            FullPath = input.FullPath,
            PlatformProvider = input.PlatformProvider,
            PlatformFolderId = input.PlatformFolderId,
            PlatformParentId = input.PlatformParentId,
            ProvisioningStatus = input.ProvisioningStatus ?? ProvisioningEvidenceStatus.Created,
            CreatedOnPlatformAt = input.CreatedOnPlatformAt,
            CreatedOnPlatformBy = input.CreatedOnPlatformBy,
            DeviationComment = input.DeviationComment,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<ProvisioningEvidenceModel>.Success(ReconciliationMapping.ToModel(created), 201, correlationId);
    }

    public async Task<Response<IReadOnlyList<ProvisioningEvidenceModel>>> ListByBaselineAsync(Guid baselineReleaseId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _evidence.GetByBaselineAsync(baselineReleaseId, ct);
        var models = rows
            .OrderBy(e => e.FullPath, StringComparer.Ordinal)
            .Select(ReconciliationMapping.ToModel)
            .ToList();
        return Response<IReadOnlyList<ProvisioningEvidenceModel>>.Success(models, 200, correlationId);
    }

    public Task<Response<ProvisioningEvidenceModel>> MarkPermissionsAppliedAsync(Guid id, string correlationId, CancellationToken ct) =>
        SignOffAsync(id, correlationId, ct, e =>
        {
            e.PermissionsApplied = true;
            e.PermissionsAppliedAt = DateTimeOffset.UtcNow;
            e.PermissionsAppliedBy = _currentUser.ActorName;
        });

    public Task<Response<ProvisioningEvidenceModel>> MarkQaVerifiedAsync(Guid id, string correlationId, CancellationToken ct) =>
        SignOffAsync(id, correlationId, ct, e =>
        {
            e.QaVerified = true;
            e.QaVerifiedAt = DateTimeOffset.UtcNow;
            e.QaVerifiedBy = _currentUser.ActorName;
        });

    private async Task<Response<ProvisioningEvidenceModel>> SignOffAsync(
        Guid id, string correlationId, CancellationToken ct, Action<DocumentCollectionProvisioningEvidence> apply)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var evidence = await _evidence.GetByIdAsync(id, ct);
        if (evidence is null)
        {
            return Response<ProvisioningEvidenceModel>.Fail(
                "Evidence not found.", 404, ReconciliationReasonCodes.NotFoundNonLeakage, correlationId);
        }

        apply(evidence);
        evidence.UpdatedBy = _currentUser.ActorName;
        await _evidence.UpdateAsync(evidence, ct);
        return Response<ProvisioningEvidenceModel>.Success(ReconciliationMapping.ToModel(evidence), 200, correlationId);
    }
}
