using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Handlers;

public sealed class EvaluateCandidateDisputeReadinessHandler
    : IRequestHandler<EvaluateCandidateDisputeReadinessCommand, Response<CandidateDisputeEvaluationDto>>
{
    private readonly ITepCandidateDisputeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateCandidateDisputeReadinessHandler(
        ITepCandidateDisputeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<CandidateDisputeEvaluationDto>> Handle(EvaluateCandidateDisputeReadinessCommand request, CancellationToken ct)
    {
        var tenant = CandidateDisputeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateDisputeEvaluationDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<CandidateDisputeEvaluationDto>.Fail("Candidate dispute readiness record was not found.", 404);
        }

        var evaluation = CandidateDisputeGuard.Evaluate(entity, request.Request.ReadinessRequested);
        if (request.Request.ReadinessRequested && !evaluation.ReadinessAllowed)
        {
            return Response<CandidateDisputeEvaluationDto>.Fail("Candidate dispute readiness evaluation failed closed.", 404);
        }

        entity.DisputeReadinessState = evaluation.ReadinessAllowed
            ? TepCandidateDisputeReadinessState.Ready
            : TepCandidateDisputeReadinessState.Deferred;
        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.UpdatedAt = evaluation.EvaluatedAt;

        if (evaluation.EvaluationDeferred)
        {
            entity.DependencyStates = CandidateDisputeGuard.BuildRequiredDependencyStates(
                TepShellDependencyStatus.Deferred,
                "Candidate dispute readiness dependency evaluation deferred.").ToList();
            entity.ResponseBoundaryState = TepCandidateResponseBoundaryState.Deferred;
            entity.DisputeIntakeState = TepDisputeIntakeState.Deferred;
            entity.DisputeReviewState = TepDisputeReviewState.Deferred;
            entity.ResolutionLifecycleState = TepResolutionLifecycleState.Deferred;
            entity.HumanReviewState = TepHumanReviewState.Deferred;
            entity.ContestabilityState = TepContestabilityState.Deferred;
            entity.ConsentPreconditionState = TepConsentRequirementState.Deferred;
            entity.VisibilityApprovalState = TepVisibilityApprovalState.Deferred;
            entity.DataScopeState = TepDataScopeState.Deferred;
            entity.EvidenceRetentionState = TepEvidenceRetentionDecisionState.Deferred;
            entity.AuditReadinessState = TepAuditReadinessState.Deferred;
            entity.LegalHoldState = TepLocalDeferredPolicyState.Deferred;
            entity.DeletionPolicyState = TepLocalDeferredPolicyState.Deferred;
            entity.SelfServiceBoundaryState = TepSelfServiceBoundaryState.Deferred;
            entity.NotificationDependencyState = TepExternalDependencyState.Deferred;
            entity.DocumentDependencyState = TepExternalDependencyState.Deferred;
            entity.AutomatedDecisionBoundaryState = TepCandidateDisputeBoundaryState.Deferred;
            entity.MarketplaceBoundaryState = TepCandidateDisputeBoundaryState.Deferred;
            entity.DeferredReason = "Candidate dispute readiness dependency evaluation deferred.";
        }

        await _repository.UpdateAsync(entity, ct);
        return Response<CandidateDisputeEvaluationDto>.Success(evaluation);
    }
}
