using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Handlers;

public sealed class EvaluateCandidateProfileHandler
    : IRequestHandler<EvaluateCandidateProfileCommand, Response<CandidateProfileEvaluationDto>>
{
    private readonly ITepCandidateProfileMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedRepository;
    private readonly ITepReviewBoardCaseMetadataRepository _reviewRepository;
    private readonly ITepTrustLevelPolicyMetadataRepository _trustRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateCandidateProfileHandler(
        ITepCandidateProfileMetadataRepository repository,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITepVerifiedParticipantAccessRepository verifiedRepository,
        ITepReviewBoardCaseMetadataRepository reviewRepository,
        ITepTrustLevelPolicyMetadataRepository trustRepository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _associationRepository = associationRepository;
        _policyRepository = policyRepository;
        _verifiedRepository = verifiedRepository;
        _reviewRepository = reviewRepository;
        _trustRepository = trustRepository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CandidateProfileEvaluationDto>> Handle(EvaluateCandidateProfileCommand request, CancellationToken ct)
    {
        var tenant = CandidateProfileGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateProfileEvaluationDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);

        var entity = await _repository.GetByIdAsync(tenantId, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<CandidateProfileEvaluationDto>.Fail("Candidate profile record was not found.", 404);
        }

        var association = entity.AssociationMembershipId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, scope, associationId, ct)
            : null;
        var policy = entity.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, scope, policyId, ct)
            : null;
        var verified = entity.VerifiedParticipantId is { } verifiedId
            ? await _verifiedRepository.GetByIdAsync(tenantId, scope, verifiedId, ct)
            : null;
        var review = entity.ReviewBoardCaseId is { } reviewId
            ? await _reviewRepository.GetByIdAsync(tenantId, scope, reviewId, ct)
            : null;
        var trust = entity.TrustLevelPolicyId is { } trustId
            ? await _trustRepository.GetByIdAsync(tenantId, scope, trustId, ct)
            : null;

        var evaluation = CandidateProfileGuard.Evaluate(entity, association, policy, verified, review, trust, request.Request.ActivationRequested);
        if (request.Request.ActivationRequested && !evaluation.ActivationAllowed)
        {
            return Response<CandidateProfileEvaluationDto>.Fail("Candidate profile evaluation failed closed.", 404);
        }

        entity.CandidateIdentityState = evaluation.ActivationAllowed ? TepCandidateIdentityState.Active : TepCandidateIdentityState.Deferred;
        entity.TalentProfileState = evaluation.ActivationAllowed ? TepTalentProfileState.Published : TepTalentProfileState.Deferred;
        entity.PolicyEvaluationState = evaluation.ActivationAllowed ? TepPolicyEvaluationState.Approved : TepPolicyEvaluationState.Deferred;
        entity.ProfilePolicyEvaluationState = evaluation.ActivationAllowed ? TepPolicyEvaluationState.Approved : TepPolicyEvaluationState.Deferred;
        entity.VisibilityApprovalState = evaluation.ActivationAllowed ? TepVisibilityApprovalState.Approved : TepVisibilityApprovalState.Deferred;
        entity.DataScopeState = evaluation.ActivationAllowed ? TepDataScopeState.Available : TepDataScopeState.Deferred;
        entity.DataMinimizationState = evaluation.ActivationAllowed ? TepDataMinimizationState.Approved : TepDataMinimizationState.Deferred;
        entity.AssociationValidationState = evaluation.ActivationAllowed ? TepShellDependencyStatus.Available : TepShellDependencyStatus.Deferred;
        entity.VerifiedAccessValidationState = evaluation.ActivationAllowed ? TepShellDependencyStatus.Available : TepShellDependencyStatus.Deferred;
        entity.ReviewBoardValidationState = evaluation.ActivationAllowed ? TepShellDependencyStatus.Available : TepShellDependencyStatus.Deferred;
        entity.TrustLevelValidationState = evaluation.ActivationAllowed ? TepShellDependencyStatus.Available : TepShellDependencyStatus.Deferred;
        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.DeferredReason = evaluation.EvaluationDeferred ? "Candidate profile dependency evaluation deferred." : entity.DeferredReason;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<CandidateProfileEvaluationDto>.Success(evaluation);
    }
}
