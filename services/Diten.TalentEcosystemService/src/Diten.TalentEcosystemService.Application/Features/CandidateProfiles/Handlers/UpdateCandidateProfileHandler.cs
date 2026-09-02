using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Handlers;

public sealed class UpdateCandidateProfileHandler : IRequestHandler<UpdateCandidateProfileCommand, Response<NoContent>>
{
    private readonly ITepCandidateProfileMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedRepository;
    private readonly ITepReviewBoardCaseMetadataRepository _reviewRepository;
    private readonly ITepTrustLevelPolicyMetadataRepository _trustRepository;
    private readonly ITenantContext _tenantContext;

    public UpdateCandidateProfileHandler(
        ITepCandidateProfileMetadataRepository repository,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITepVerifiedParticipantAccessRepository verifiedRepository,
        ITepReviewBoardCaseMetadataRepository reviewRepository,
        ITepTrustLevelPolicyMetadataRepository trustRepository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _associationRepository = associationRepository;
        _policyRepository = policyRepository;
        _verifiedRepository = verifiedRepository;
        _reviewRepository = reviewRepository;
        _trustRepository = trustRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(UpdateCandidateProfileCommand request, CancellationToken ct)
    {
        var tenant = CandidateProfileGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<NoContent>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Candidate profile record was not found.", 404);
        }

        var validation = CandidateProfileGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<NoContent>.Fail(validation, 400);
        }

        var activation = CandidateProfileGuard.ValidateActivationRequest(request.Request);
        if (!activation.IsSuccessful)
        {
            return Response<NoContent>.Fail(activation.Errors, activation.StatusCode);
        }

        if (CandidateProfileGuard.IsActivationRequested(request.Request.CandidateIdentityState, request.Request.TalentProfileState)
            && !await DependenciesAllowActivationAsync(tenantId, BuildCandidateProfileMetadata(request.Id, tenantId, request.Request), ct))
        {
            return Response<NoContent>.Fail("Candidate profile activation requires same-tenant dependency preconditions.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, request.Request.Code.Trim(), request.Id, ct))
        {
            return Response<NoContent>.Fail("An active candidate profile record with the same Code already exists for this tenant.", 409);
        }

        entity.Code = request.Request.Code.Trim();
        entity.DisplayName = request.Request.DisplayName.Trim();
        entity.CandidateReference = request.Request.CandidateReference.Trim();
        entity.TalentProfileReference = request.Request.TalentProfileReference.Trim();
        entity.AssociationMembershipId = request.Request.AssociationMembershipId;
        entity.ConsentVisibilityPolicyId = request.Request.ConsentVisibilityPolicyId;
        entity.VerifiedParticipantId = request.Request.VerifiedParticipantId;
        entity.ReviewBoardCaseId = request.Request.ReviewBoardCaseId;
        entity.TrustLevelPolicyId = request.Request.TrustLevelPolicyId;
        entity.HcmFoundationReference = request.Request.HcmFoundationReference.Trim();
        entity.SkillSummaryMetadata = request.Request.SkillSummaryMetadata.Trim();
        entity.CredentialSummaryMetadata = request.Request.CredentialSummaryMetadata.Trim();
        entity.ExperienceSummaryMetadata = request.Request.ExperienceSummaryMetadata.Trim();
        entity.CandidateIdentityState = request.Request.CandidateIdentityState;
        entity.TalentProfileState = request.Request.TalentProfileState;
        entity.ProfileCompletenessState = request.Request.ProfileCompletenessState;
        entity.VisibilityClassification = request.Request.VisibilityClassification;
        entity.ConsentBasisState = request.Request.ConsentBasisState;
        entity.PolicyEvaluationState = request.Request.PolicyEvaluationState;
        entity.ProfilePolicyEvaluationState = request.Request.ProfilePolicyEvaluationState;
        entity.VisibilityApprovalState = request.Request.VisibilityApprovalState;
        entity.DataScopeState = request.Request.DataScopeState;
        entity.DataMinimizationState = request.Request.DataMinimizationState;
        entity.AssociationValidationState = request.Request.AssociationValidationState;
        entity.VerifiedAccessValidationState = request.Request.VerifiedAccessValidationState;
        entity.ReviewBoardValidationState = request.Request.ReviewBoardValidationState;
        entity.TrustLevelValidationState = request.Request.TrustLevelValidationState;
        entity.HcmValidationState = request.Request.HcmValidationState;
        entity.DependencyStates = request.Request.DependencyStates.Select(CandidateProfileMapper.ToEntity).ToList();
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.LastEvaluatedAt = request.Request.LastEvaluatedAt;
        entity.CandidateVersion = request.Request.CandidateVersion;
        entity.LocalAuditEvidenceRetentionState = request.Request.LocalAuditEvidenceRetentionState;
        entity.DeferredReason = string.IsNullOrWhiteSpace(request.Request.DeferredReason) ? null : request.Request.DeferredReason.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }

    private async Task<bool> DependenciesAllowActivationAsync(Guid tenantId, TepCandidateProfileMetadata entity, CancellationToken ct)
    {
        var association = entity.AssociationMembershipId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, associationId, ct)
            : null;
        var policy = entity.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, policyId, ct)
            : null;
        var verified = entity.VerifiedParticipantId is { } verifiedId
            ? await _verifiedRepository.GetByIdAsync(tenantId, verifiedId, ct)
            : null;
        var review = entity.ReviewBoardCaseId is { } reviewId
            ? await _reviewRepository.GetByIdAsync(tenantId, reviewId, ct)
            : null;
        var trust = entity.TrustLevelPolicyId is { } trustId
            ? await _trustRepository.GetByIdAsync(tenantId, trustId, ct)
            : null;

        return CandidateProfileGuard.AssociationAllowsCandidate(entity, association)
            && CandidateProfileGuard.PolicyAllowsCandidate(policy)
            && CandidateProfileGuard.VerifiedAccessAllowsCandidate(entity, verified)
            && CandidateProfileGuard.ReviewBoardAllowsCandidate(entity, review)
            && CandidateProfileGuard.TrustLevelAllowsCandidate(entity, trust);
    }

    private static TepCandidateProfileMetadata BuildCandidateProfileMetadata(Guid id, Guid tenantId, CandidateProfileRequest request) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            AssociationMembershipId = request.AssociationMembershipId,
            ConsentVisibilityPolicyId = request.ConsentVisibilityPolicyId,
            VerifiedParticipantId = request.VerifiedParticipantId,
            ReviewBoardCaseId = request.ReviewBoardCaseId,
            TrustLevelPolicyId = request.TrustLevelPolicyId,
            ConsentBasisState = request.ConsentBasisState,
            PolicyEvaluationState = request.PolicyEvaluationState,
            ProfilePolicyEvaluationState = request.ProfilePolicyEvaluationState,
            VisibilityApprovalState = request.VisibilityApprovalState,
            DataScopeState = request.DataScopeState,
            DataMinimizationState = request.DataMinimizationState,
            ProfileCompletenessState = request.ProfileCompletenessState,
            VisibilityClassification = request.VisibilityClassification,
            AssociationValidationState = request.AssociationValidationState,
            VerifiedAccessValidationState = request.VerifiedAccessValidationState,
            ReviewBoardValidationState = request.ReviewBoardValidationState,
            TrustLevelValidationState = request.TrustLevelValidationState,
            HcmValidationState = request.HcmValidationState
        };
}
