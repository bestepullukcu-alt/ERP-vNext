using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Commands;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Handlers;

public sealed class CreateScorecardsDashboardsReadinessHandler : IRequestHandler<CreateScorecardsDashboardsReadinessCommand, Response<Guid>>
{
    private readonly IScorecardsDashboardsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateScorecardsDashboardsReadinessHandler(IScorecardsDashboardsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateScorecardsDashboardsReadinessCommand request, CancellationToken ct)
    {
        var tenant = ScorecardsDashboardsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = ScorecardsDashboardsGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = ScorecardsDashboardsGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active scorecards-dashboards readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = ScorecardsDashboardsGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new ScorecardsDashboardsReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            ScorecardsDashboardsReadinessState = readinessState,
            ScorecardCatalogBoundaryState = request.Request.ScorecardCatalogBoundaryState,
            WidgetBindingIntakeBoundaryState = request.Request.WidgetBindingIntakeBoundaryState,
            LayoutScopeBoundaryState = request.Request.LayoutScopeBoundaryState,
            PublicationControlBoundaryState = request.Request.PublicationControlBoundaryState,
            DashboardReviewBoundaryState = request.Request.DashboardReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            MetricSemanticRegistrySourceDependencyState = request.Request.MetricSemanticRegistrySourceDependencyState,
            DataWarehouseSourceDependencyState = request.Request.DataWarehouseSourceDependencyState,
            DataContractRegistryDependencyState = request.Request.DataContractRegistryDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            StewardshipPreconditionState = request.Request.StewardshipPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, ScorecardsDashboardsReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            ScorecardsDashboardsReadinessVersion = request.Request.ScorecardsDashboardsReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = ScorecardsDashboardsGuard.MergeDeferredReason(
                ScorecardsDashboardsGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == ScorecardsDashboardsReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
