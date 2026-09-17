using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Handlers;

public sealed class CreateHrKpiAnalyticsReadinessHandler : IRequestHandler<CreateHrKpiAnalyticsReadinessCommand, Response<Guid>>
{
    private readonly IHrKpiAnalyticsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateHrKpiAnalyticsReadinessHandler(IHrKpiAnalyticsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateHrKpiAnalyticsReadinessCommand request, CancellationToken ct)
    {
        var tenant = HrKpiAnalyticsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        if (!await _legalEntityContext.IsSelectionAllowedAsync(ct))
        {
            return Response<Guid>.Fail(
                "A permitted legal entity must be selected (X-Legal-Entity-Id) to create this record.",
                403);
        }

        var errors = HrKpiAnalyticsGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = HrKpiAnalyticsGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active hr-kpi-analytics readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = HrKpiAnalyticsGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new HrKpiAnalyticsReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            HrKpiAnalyticsReadinessState = readinessState,
            KpiCatalogBoundaryState = request.Request.KpiCatalogBoundaryState,
            MetricDefinitionBoundaryState = request.Request.MetricDefinitionBoundaryState,
            DashboardBoundaryState = request.Request.DashboardBoundaryState,
            AnalyticsQueryBoundaryState = request.Request.AnalyticsQueryBoundaryState,
            DataExportBoundaryState = request.Request.DataExportBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            AnalyticsPlatformDependencyState = request.Request.AnalyticsPlatformDependencyState,
            DataSourceDependencyState = request.Request.DataSourceDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, HrKpiAnalyticsReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            HrKpiAnalyticsReadinessVersion = request.Request.HrKpiAnalyticsReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = HrKpiAnalyticsGuard.MergeDeferredReason(
                HrKpiAnalyticsGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == HrKpiAnalyticsReadinessState.Deferred
                    ? "HR KPI and analytics readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
