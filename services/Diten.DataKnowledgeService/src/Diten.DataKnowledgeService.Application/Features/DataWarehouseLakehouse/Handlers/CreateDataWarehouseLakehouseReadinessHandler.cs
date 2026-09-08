using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Commands;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Handlers;

public sealed class CreateDataWarehouseLakehouseReadinessHandler : IRequestHandler<CreateDataWarehouseLakehouseReadinessCommand, Response<Guid>>
{
    private readonly IDataWarehouseLakehouseReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateDataWarehouseLakehouseReadinessHandler(IDataWarehouseLakehouseReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateDataWarehouseLakehouseReadinessCommand request, CancellationToken ct)
    {
        var tenant = DataWarehouseLakehouseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = DataWarehouseLakehouseGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = DataWarehouseLakehouseGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active data-warehouse-lakehouse readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = DataWarehouseLakehouseGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new DataWarehouseLakehouseReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            DataWarehouseLakehouseReadinessState = readinessState,
            StorageLayerCatalogBoundaryState = request.Request.StorageLayerCatalogBoundaryState,
            IngestionIntakeBoundaryState = request.Request.IngestionIntakeBoundaryState,
            PartitioningScopeBoundaryState = request.Request.PartitioningScopeBoundaryState,
            LineageControlBoundaryState = request.Request.LineageControlBoundaryState,
            WarehouseReviewBoundaryState = request.Request.WarehouseReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            VaultDependencyState = request.Request.VaultDependencyState,
            LoggingMonitoringDependencyState = request.Request.LoggingMonitoringDependencyState,
            DataContractRegistryDependencyState = request.Request.DataContractRegistryDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            StewardshipPreconditionState = request.Request.StewardshipPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            StorageTierPolicyState = request.Request.StorageTierPolicyState,
            DependencyStates = new Dictionary<string, DataWarehouseLakehouseReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            DataWarehouseLakehouseReadinessVersion = request.Request.DataWarehouseLakehouseReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = DataWarehouseLakehouseGuard.MergeDeferredReason(
                DataWarehouseLakehouseGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == DataWarehouseLakehouseReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
