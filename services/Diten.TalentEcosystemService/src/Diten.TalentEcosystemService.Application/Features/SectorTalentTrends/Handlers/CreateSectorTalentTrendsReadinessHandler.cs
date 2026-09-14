using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Handlers;

public sealed class CreateSectorTalentTrendsReadinessHandler : IRequestHandler<CreateSectorTalentTrendsReadinessCommand, Response<Guid>>
{
    private readonly ISectorTalentTrendsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateSectorTalentTrendsReadinessHandler(ISectorTalentTrendsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateSectorTalentTrendsReadinessCommand request, CancellationToken ct)
    {
        var tenant = SectorTalentTrendsGuard.RequireTenant(_tenantContext);
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

        var errors = SectorTalentTrendsGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = SectorTalentTrendsGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active sector-talent-trends readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = SectorTalentTrendsGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new SectorTalentTrendsReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            SectorTalentTrendsReadinessState = readinessState,
            TrendCatalogBoundaryState = request.Request.TrendCatalogBoundaryState,
            SignalBindingIntakeBoundaryState = request.Request.SignalBindingIntakeBoundaryState,
            AggregationScopeBoundaryState = request.Request.AggregationScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            TrendReviewBoundaryState = request.Request.TrendReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            WorkforceAnalyticsSourceDependencyState = request.Request.WorkforceAnalyticsSourceDependencyState,
            DataGovernancePolicyDependencyState = request.Request.DataGovernancePolicyDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, SectorTalentTrendsReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            SectorTalentTrendsReadinessVersion = request.Request.SectorTalentTrendsReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = SectorTalentTrendsGuard.MergeDeferredReason(
                SectorTalentTrendsGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == SectorTalentTrendsReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
