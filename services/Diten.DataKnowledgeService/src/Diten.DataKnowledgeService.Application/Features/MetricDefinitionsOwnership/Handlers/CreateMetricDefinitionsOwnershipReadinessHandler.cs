using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Commands;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Handlers;

public sealed class CreateMetricDefinitionsOwnershipReadinessHandler : IRequestHandler<CreateMetricDefinitionsOwnershipReadinessCommand, Response<Guid>>
{
    private readonly IMetricDefinitionsOwnershipReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateMetricDefinitionsOwnershipReadinessHandler(IMetricDefinitionsOwnershipReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateMetricDefinitionsOwnershipReadinessCommand request, CancellationToken ct)
    {
        var tenant = MetricDefinitionsOwnershipGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = MetricDefinitionsOwnershipGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = MetricDefinitionsOwnershipGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active metric-definitions-ownership readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = MetricDefinitionsOwnershipGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new MetricDefinitionsOwnershipReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            MetricDefinitionsOwnershipReadinessState = readinessState,
            DefinitionCatalogBoundaryState = request.Request.DefinitionCatalogBoundaryState,
            OwnershipAssignmentIntakeBoundaryState = request.Request.OwnershipAssignmentIntakeBoundaryState,
            StewardshipScopeBoundaryState = request.Request.StewardshipScopeBoundaryState,
            ApprovalControlBoundaryState = request.Request.ApprovalControlBoundaryState,
            DefinitionReviewBoundaryState = request.Request.DefinitionReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            MetricSemanticRegistrySourceDependencyState = request.Request.MetricSemanticRegistrySourceDependencyState,
            KpiCatalogSourceDependencyState = request.Request.KpiCatalogSourceDependencyState,
            DataContractRegistryDependencyState = request.Request.DataContractRegistryDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            StewardshipPreconditionState = request.Request.StewardshipPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            ApprovalPolicyState = request.Request.ApprovalPolicyState,
            DependencyStates = new Dictionary<string, MetricDefinitionsOwnershipReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            MetricDefinitionsOwnershipReadinessVersion = request.Request.MetricDefinitionsOwnershipReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = MetricDefinitionsOwnershipGuard.MergeDeferredReason(
                MetricDefinitionsOwnershipGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == MetricDefinitionsOwnershipReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
