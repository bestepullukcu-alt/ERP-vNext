using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Handlers;

public sealed class EvaluateReferenceExchangeReadinessHandler
    : IRequestHandler<EvaluateReferenceExchangeReadinessCommand, Response<ReferenceExchangeEvaluationDto>>
{
    private readonly ITepReferenceExchangeMarketplaceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateReferenceExchangeReadinessHandler(
        ITepReferenceExchangeMarketplaceReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<ReferenceExchangeEvaluationDto>> Handle(EvaluateReferenceExchangeReadinessCommand request, CancellationToken ct)
    {
        var tenant = ReferenceExchangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ReferenceExchangeEvaluationDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<ReferenceExchangeEvaluationDto>.Fail("Reference exchange readiness record was not found.", 404);
        }

        var evaluation = ReferenceExchangeGuard.Evaluate(entity, request.Request.ReadinessRequested);
        if (request.Request.ReadinessRequested && !evaluation.ReadinessAllowed)
        {
            return Response<ReferenceExchangeEvaluationDto>.Fail("Reference exchange readiness evaluation failed closed.", 404);
        }

        entity.ExchangeReadinessState = evaluation.ReadinessAllowed
            ? TepReferenceExchangeReadinessState.Ready
            : TepReferenceExchangeReadinessState.Deferred;
        entity.ExchangeAvailabilityState = evaluation.ReadinessAllowed
            ? TepReferenceExchangeAvailabilityState.LocalMetadata
            : TepReferenceExchangeAvailabilityState.Deferred;
        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.UpdatedAt = evaluation.EvaluatedAt;

        if (evaluation.EvaluationDeferred)
        {
            entity.DependencyStates = ReferenceExchangeGuard.BuildRequiredDependencyStates(
                TepShellDependencyStatus.Deferred,
                "Reference exchange readiness dependency evaluation deferred.").ToList();
            entity.EvidenceRetentionState = TepEvidenceRetentionDecisionState.Deferred;
            entity.AuditReadinessState = TepAuditReadinessState.Deferred;
            entity.AbuseControlState = TepAbuseControlState.Deferred;
            entity.ThrottlingPolicyState = TepThrottlingPolicyState.Deferred;
            entity.DeferredReason = "Reference exchange readiness dependency evaluation deferred.";
        }

        await _repository.UpdateAsync(entity, ct);
        return Response<ReferenceExchangeEvaluationDto>.Success(evaluation);
    }
}
