using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Commands;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Handlers;

public sealed class CreateMetricSemanticRegistryReadinessHandler : IRequestHandler<CreateMetricSemanticRegistryReadinessCommand, Response<Guid>>
{
    private readonly IMetricSemanticRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateMetricSemanticRegistryReadinessHandler(IMetricSemanticRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateMetricSemanticRegistryReadinessCommand request, CancellationToken ct)
    {
        var tenant = MetricSemanticRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = MetricSemanticRegistryGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = MetricSemanticRegistryGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active metric-semantic-registry readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = MetricSemanticRegistryGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new MetricSemanticRegistryReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            MetricSemanticRegistryReadinessState = readinessState,
            MetricIdentityCatalogBoundaryState = request.Request.MetricIdentityCatalogBoundaryState,
            SemanticEntityIntakeBoundaryState = request.Request.SemanticEntityIntakeBoundaryState,
            DimensionMeasureScopeBoundaryState = request.Request.DimensionMeasureScopeBoundaryState,
            SemanticBindingControlBoundaryState = request.Request.SemanticBindingControlBoundaryState,
            RegistryReviewBoundaryState = request.Request.RegistryReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            DataSourceRegistryDependencyState = request.Request.DataSourceRegistryDependencyState,
            DataGovernancePolicyDependencyState = request.Request.DataGovernancePolicyDependencyState,
            SemanticContractSourceDependencyState = request.Request.SemanticContractSourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            StewardshipPreconditionState = request.Request.StewardshipPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            VersioningPolicyState = request.Request.VersioningPolicyState,
            DependencyStates = new Dictionary<string, MetricSemanticRegistryReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            MetricSemanticRegistryReadinessVersion = request.Request.MetricSemanticRegistryReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = MetricSemanticRegistryGuard.MergeDeferredReason(
                MetricSemanticRegistryGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == MetricSemanticRegistryReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
