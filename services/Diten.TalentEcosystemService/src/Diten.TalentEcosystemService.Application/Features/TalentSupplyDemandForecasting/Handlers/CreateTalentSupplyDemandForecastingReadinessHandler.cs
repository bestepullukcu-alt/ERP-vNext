using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Handlers;

public sealed class CreateTalentSupplyDemandForecastingReadinessHandler : IRequestHandler<CreateTalentSupplyDemandForecastingReadinessCommand, Response<Guid>>
{
    private readonly ITalentSupplyDemandForecastingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateTalentSupplyDemandForecastingReadinessHandler(ITalentSupplyDemandForecastingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateTalentSupplyDemandForecastingReadinessCommand request, CancellationToken ct)
    {
        var tenant = TalentSupplyDemandForecastingGuard.RequireTenant(_tenantContext);
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

        var errors = TalentSupplyDemandForecastingGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = TalentSupplyDemandForecastingGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active talent-supply-demand-forecasting readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = TalentSupplyDemandForecastingGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new TalentSupplyDemandForecastingReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            TalentSupplyDemandForecastingReadinessState = readinessState,
            ForecastCatalogBoundaryState = request.Request.ForecastCatalogBoundaryState,
            ModelBindingIntakeBoundaryState = request.Request.ModelBindingIntakeBoundaryState,
            HorizonScopeBoundaryState = request.Request.HorizonScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            ForecastReviewBoundaryState = request.Request.ForecastReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            WorkforceAnalyticsSourceDependencyState = request.Request.WorkforceAnalyticsSourceDependencyState,
            SectorTrendSourceDependencyState = request.Request.SectorTrendSourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, TalentSupplyDemandForecastingReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            TalentSupplyDemandForecastingReadinessVersion = request.Request.TalentSupplyDemandForecastingReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = TalentSupplyDemandForecastingGuard.MergeDeferredReason(
                TalentSupplyDemandForecastingGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == TalentSupplyDemandForecastingReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
