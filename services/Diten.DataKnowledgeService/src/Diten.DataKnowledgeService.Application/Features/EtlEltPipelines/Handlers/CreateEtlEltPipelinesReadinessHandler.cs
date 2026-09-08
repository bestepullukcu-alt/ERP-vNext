using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Commands;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Handlers;

public sealed class CreateEtlEltPipelinesReadinessHandler : IRequestHandler<CreateEtlEltPipelinesReadinessCommand, Response<Guid>>
{
    private readonly IEtlEltPipelinesReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateEtlEltPipelinesReadinessHandler(IEtlEltPipelinesReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateEtlEltPipelinesReadinessCommand request, CancellationToken ct)
    {
        var tenant = EtlEltPipelinesGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = EtlEltPipelinesGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = EtlEltPipelinesGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active etl-elt-pipelines readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = EtlEltPipelinesGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new EtlEltPipelinesReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            EtlEltPipelinesReadinessState = readinessState,
            PipelineDefinitionCatalogBoundaryState = request.Request.PipelineDefinitionCatalogBoundaryState,
            ExtractIntakeBoundaryState = request.Request.ExtractIntakeBoundaryState,
            TransformScopeBoundaryState = request.Request.TransformScopeBoundaryState,
            LoadControlBoundaryState = request.Request.LoadControlBoundaryState,
            OrchestrationReviewBoundaryState = request.Request.OrchestrationReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            LakehouseSourceDependencyState = request.Request.LakehouseSourceDependencyState,
            JobOrchestrationDependencyState = request.Request.JobOrchestrationDependencyState,
            DataContractRegistryDependencyState = request.Request.DataContractRegistryDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            StewardshipPreconditionState = request.Request.StewardshipPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            MonitoringPolicyState = request.Request.MonitoringPolicyState,
            DependencyStates = new Dictionary<string, EtlEltPipelinesReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            EtlEltPipelinesReadinessVersion = request.Request.EtlEltPipelinesReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = EtlEltPipelinesGuard.MergeDeferredReason(
                EtlEltPipelinesGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == EtlEltPipelinesReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
