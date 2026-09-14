using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Handlers;

public sealed class CreateHiringRiskIndicatorsReadinessHandler : IRequestHandler<CreateHiringRiskIndicatorsReadinessCommand, Response<Guid>>
{
    private readonly IHiringRiskIndicatorsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateHiringRiskIndicatorsReadinessHandler(IHiringRiskIndicatorsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateHiringRiskIndicatorsReadinessCommand request, CancellationToken ct)
    {
        var tenant = HiringRiskIndicatorsGuard.RequireTenant(_tenantContext);
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

        var errors = HiringRiskIndicatorsGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = HiringRiskIndicatorsGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active hiring-risk-indicators readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = HiringRiskIndicatorsGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new HiringRiskIndicatorsReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            HiringRiskIndicatorsReadinessState = readinessState,
            RiskIndicatorCatalogBoundaryState = request.Request.RiskIndicatorCatalogBoundaryState,
            RiskSignalIntakeBoundaryState = request.Request.RiskSignalIntakeBoundaryState,
            RiskAssessmentBoundaryState = request.Request.RiskAssessmentBoundaryState,
            MitigationTrackingBoundaryState = request.Request.MitigationTrackingBoundaryState,
            IndicatorReviewBoundaryState = request.Request.IndicatorReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, HiringRiskIndicatorsReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            HiringRiskIndicatorsReadinessVersion = request.Request.HiringRiskIndicatorsReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = HiringRiskIndicatorsGuard.MergeDeferredReason(
                HiringRiskIndicatorsGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == HiringRiskIndicatorsReadinessState.Deferred
                    ? "Hiring risk indicators readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
