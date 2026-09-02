using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Handlers;

public sealed class EvaluateRehireRecommendationReadinessHandler
    : IRequestHandler<EvaluateRehireRecommendationReadinessCommand, Response<RehireRecommendationEvaluationDto>>
{
    private readonly ITepRehireRecommendationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateRehireRecommendationReadinessHandler(
        ITepRehireRecommendationReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<RehireRecommendationEvaluationDto>> Handle(EvaluateRehireRecommendationReadinessCommand request, CancellationToken ct)
    {
        var tenant = RehireRecommendationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<RehireRecommendationEvaluationDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<RehireRecommendationEvaluationDto>.Fail("Rehire recommendation readiness record was not found.", 404);
        }

        var evaluation = RehireRecommendationGuard.Evaluate(entity, request.Request.ReadinessRequested);
        if (request.Request.ReadinessRequested && !evaluation.ReadinessAllowed)
        {
            return Response<RehireRecommendationEvaluationDto>.Fail("Rehire recommendation readiness evaluation failed closed.", 404);
        }

        entity.RecommendationReadinessState = evaluation.ReadinessAllowed
            ? TepRehireRecommendationReadinessState.Ready
            : TepRehireRecommendationReadinessState.Deferred;
        entity.RecommendationEvaluationState = evaluation.ReadinessAllowed
            ? TepRehireRecommendationEvaluationState.Ready
            : TepRehireRecommendationEvaluationState.Deferred;
        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.UpdatedAt = evaluation.EvaluatedAt;

        if (evaluation.EvaluationDeferred)
        {
            entity.DependencyStates = RehireRecommendationGuard.BuildRequiredDependencyStates(
                TepShellDependencyStatus.Deferred,
                "Rehire recommendation readiness dependency evaluation deferred.").ToList();
            entity.RecommendationPolicyState = TepRehireRecommendationPolicyState.Deferred;
            entity.EligibilityPreconditionState = TepRecommendationEligibilityPreconditionState.Deferred;
            entity.ExplainabilityState = TepRecommendationExplainabilityState.Deferred;
            entity.HumanReviewState = TepHumanReviewState.Deferred;
            entity.ContestabilityState = TepContestabilityState.Deferred;
            entity.AbuseControlState = TepAbuseControlState.Deferred;
            entity.MisuseDetectionState = TepMisuseDetectionState.Deferred;
            entity.ThrottlingState = TepThrottlingPolicyState.Deferred;
            entity.EscalationState = TepEscalationState.Deferred;
            entity.EvidenceRetentionState = TepEvidenceRetentionDecisionState.Deferred;
            entity.AuditReadinessState = TepAuditReadinessState.Deferred;
            entity.LegalHoldState = TepLocalDeferredPolicyState.Deferred;
            entity.DeletionPolicyState = TepLocalDeferredPolicyState.Deferred;
            entity.DeferredReason = "Rehire recommendation readiness dependency evaluation deferred.";
        }

        await _repository.UpdateAsync(entity, ct);
        return Response<RehireRecommendationEvaluationDto>.Success(evaluation);
    }
}
