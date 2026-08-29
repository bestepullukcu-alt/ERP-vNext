using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances;

public sealed class CorporateCollectionInstanceProvisioningService
{
    private readonly IBaselineReleaseRepository _baselines;
    private readonly ICollectionDefinitionRepository _definitions;
    private readonly ICollectionInstanceRepository _instances;
    private readonly ICorporateCollectionProvisioningOperationRepository _operations;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly CorporateCollectionStoragePartitionBuilder _partitions;

    public CorporateCollectionInstanceProvisioningService(
        IBaselineReleaseRepository baselines,
        ICollectionDefinitionRepository definitions,
        ICollectionInstanceRepository instances,
        ICorporateCollectionProvisioningOperationRepository operations,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        CorporateCollectionStoragePartitionBuilder partitions)
    {
        _baselines = baselines;
        _definitions = definitions;
        _instances = instances;
        _operations = operations;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _partitions = partitions;
    }

    public async Task<Response<CorporateCollectionProvisioningResult>> ProvisionAsync(
        Guid baselineReleaseId,
        Guid corporateOwnerId,
        string idempotencyKey,
        string? displayName,
        string? description,
        string correlationId,
        CancellationToken ct)
    {
        var baseline = await _baselines.GetByIdAsync(baselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<CorporateCollectionProvisioningResult>.Fail(
                "Baseline not found.", 404, CorporateCollectionInstanceReasonCodes.NotFoundNonLeakage, correlationId);
        }
        if (!baseline.Status.IsInstantiable())
        {
            return Response<CorporateCollectionProvisioningResult>.Fail(
                "Baseline is not eligible for provisioning.", 409, CorporateCollectionInstanceReasonCodes.BaselineNotEligible, correlationId);
        }

        var operation = await _operations.CreateOrGetAsync(new CorporateCollectionInstanceProvisioningOperation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            IdempotencyKey = idempotencyKey.Trim(),
            BaselineReleaseId = baselineReleaseId,
            CorporateOwnerId = corporateOwnerId,
            ScopeOwnerId = corporateOwnerId,
            CorrelationId = correlationId,
            DisplayName = displayName?.Trim(),
            Description = description?.Trim()
        }, ct);

        if (operation.BaselineReleaseId != baselineReleaseId || operation.CorporateOwnerId != corporateOwnerId)
        {
            return Response<CorporateCollectionProvisioningResult>.Fail(
                "Idempotency key is already bound to another scope.", 409,
                CorporateCollectionInstanceReasonCodes.ValidationFailed, correlationId);
        }

        var existing = await _instances.GetCorporateAsync(baselineReleaseId, corporateOwnerId, ct);
        var active = existing.Where(x => x.InstanceStatus != CollectionInstanceStatus.Archived).OrderBy(x => x.FullPath).ToList();
        if (active.Count > 0)
        {
            var root = active.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.ParentCanonicalId)) ?? active[0];
            await CompleteAsync(operation, root.Id, ct);
            return Response<CorporateCollectionProvisioningResult>.Success(
                Result(operation, root.Id, active.Count, true, correlationId), correlationId: correlationId);
        }

        try
        {
            var definitions = (await _definitions.GetByBaselineAsync(baselineReleaseId, ct))
                .Where(x => x.Status == CollectionDefinitionStatus.Active)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.FullPath, StringComparer.Ordinal)
                .ToList();
            if (definitions.Count == 0)
            {
                return await FailAsync(operation, "BASELINE_EMPTY", "Baseline contains no active folders.", correlationId, ct);
            }

            var now = DateTimeOffset.UtcNow;
            var actor = _currentUser.UserId == Guid.Empty ? _currentUser.ActorName : _currentUser.UserId.ToString("D");
            var nodes = definitions.Select(definition =>
            {
                var id = Guid.NewGuid();
                return new CollectionInstance
                {
                    Id = id,
                    TenantId = _tenantContext.TenantId,
                    InstanceKey = $"{_tenantContext.TenantId:D}|corporate|{corporateOwnerId:D}|{baselineReleaseId:D}|{definition.CanonicalId}",
                    ScopeOwnerId = corporateOwnerId,
                    CorporateOwnerId = corporateOwnerId,
                    BaselineReleaseId = baselineReleaseId,
                    CanonicalId = definition.CanonicalId,
                    ParentCanonicalId = definition.ParentCanonicalId,
                    Name = definition.Name,
                    FullPath = definition.FullPath,
                    DisplayOrder = definition.DisplayOrder,
                    CollectionScopeType = CollectionScopeType.Corporate,
                    InstanceStatus = CollectionInstanceStatus.Active,
                    SourceDefinitionHash = definition.DefinitionHash,
                    ProvisionedFromBaselineReleaseId = baselineReleaseId,
                    ProvisionedAt = now,
                    ProvisionedBy = actor,
                    ProvisioningOperationId = operation.Id,
                    StoragePartition = _partitions.ForCorporate(corporateOwnerId, id)
                };
            }).ToList();

            operation.Status = CorporateCollectionProvisioningStatus.InstanceCreated;
            await _operations.UpdateAsync(operation, ct);
            var created = await _instances.CreateCorporateTreeIfAbsentAsync(baselineReleaseId, corporateOwnerId, nodes, ct);
            var createdActive = created.Where(x => x.InstanceStatus != CollectionInstanceStatus.Archived).OrderBy(x => x.FullPath).ToList();
            var root = createdActive.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.ParentCanonicalId)) ?? createdActive[0];
            operation.Status = CorporateCollectionProvisioningStatus.FolderTreeMaterialized;
            await _operations.UpdateAsync(operation, ct);
            await CompleteAsync(operation, root.Id, ct);
            return Response<CorporateCollectionProvisioningResult>.Success(
                Result(operation, root.Id, createdActive.Count, false, correlationId), 201, correlationId);
        }
        catch (Exception)
        {
            var raced = (await _instances.GetCorporateAsync(baselineReleaseId, corporateOwnerId, ct))
                .Where(x => x.InstanceStatus != CollectionInstanceStatus.Archived)
                .OrderBy(x => x.FullPath)
                .ToList();
            if (raced.Count > 0)
            {
                var root = raced.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.ParentCanonicalId)) ?? raced[0];
                await CompleteAsync(operation, root.Id, ct);
                return Response<CorporateCollectionProvisioningResult>.Success(
                    Result(operation, root.Id, raced.Count, true, correlationId), correlationId: correlationId);
            }

            return await FailAsync(operation, "PROVISIONING_FAILED",
                "Corporate collection provisioning failed.", correlationId, ct);
        }
    }

    public Task<Response<CorporateCollectionProvisioningResult>> RetryAsync(
        CorporateCollectionInstanceProvisioningOperation operation,
        string correlationId,
        CancellationToken ct) =>
        ProvisionAsync(operation.BaselineReleaseId, operation.CorporateOwnerId, operation.IdempotencyKey,
            operation.DisplayName, operation.Description, correlationId, ct);

    private async Task CompleteAsync(CorporateCollectionInstanceProvisioningOperation operation, Guid rootId, CancellationToken ct)
    {
        operation.CollectionInstanceId = rootId;
        operation.Status = CorporateCollectionProvisioningStatus.Completed;
        operation.CompletedAt = DateTimeOffset.UtcNow;
        operation.LastAttemptAt = DateTimeOffset.UtcNow;
        operation.FailureReasonCode = null;
        operation.FailureDetail = null;
        await _operations.UpdateAsync(operation, ct);
    }

    private async Task<Response<CorporateCollectionProvisioningResult>> FailAsync(
        CorporateCollectionInstanceProvisioningOperation operation,
        string reason,
        string detail,
        string correlationId,
        CancellationToken ct)
    {
        operation.Status = CorporateCollectionProvisioningStatus.Failed;
        operation.FailureReasonCode = reason;
        operation.FailureDetail = detail;
        operation.LastAttemptAt = DateTimeOffset.UtcNow;
        await _operations.UpdateAsync(operation, ct);
        return Response<CorporateCollectionProvisioningResult>.Fail(
            detail, 409, CorporateCollectionInstanceReasonCodes.ProvisioningFailed, correlationId);
    }

    private static CorporateCollectionProvisioningResult Result(
        CorporateCollectionInstanceProvisioningOperation operation,
        Guid rootId,
        int count,
        bool replay,
        string correlationId) =>
        new(operation.Id, rootId, operation.BaselineReleaseId, operation.CorporateOwnerId,
            CollectionScopeType.Corporate.ToString().ToUpperInvariant(), operation.Status.ToString().ToUpperInvariant(),
            count, replay, correlationId);
}
