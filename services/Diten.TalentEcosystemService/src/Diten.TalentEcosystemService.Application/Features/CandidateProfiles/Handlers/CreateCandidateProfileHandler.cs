using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Handlers;

public sealed class CreateCandidateProfileHandler : IRequestHandler<CreateCandidateProfileCommand, Response<Guid>>
{
    private readonly ITepCandidateProfileMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedRepository;
    private readonly ITepReviewBoardCaseMetadataRepository _reviewRepository;
    private readonly ITepTrustLevelPolicyMetadataRepository _trustRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateCandidateProfileHandler(
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

    public async Task<Response<Guid>> Handle(CreateCandidateProfileCommand request, CancellationToken ct)
    {
        var tenant = CandidateProfileGuard.RequireTenant(_tenantContext);
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
        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);

        var validation = CandidateProfileGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<Guid>.Fail(validation, 400);
        }

        var activation = CandidateProfileGuard.ValidateActivationRequest(request.Request);
        if (!activation.IsSuccessful)
        {
            return Response<Guid>.Fail(activation.Errors, activation.StatusCode);
        }

        if (CandidateProfileGuard.IsActivationRequested(request.Request.CandidateIdentityState, request.Request.TalentProfileState)
            && !await DependenciesAllowActivationAsync(tenantId, scope, ToEntity(tenantId, legalEntityId, request.Request), ct))
        {
            return Response<Guid>.Fail("Candidate profile activation requires same-tenant dependency preconditions.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, request.Request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("An active candidate profile record with the same Code already exists for this tenant.", 409);
        }

        var entity = ToEntity(tenantId, legalEntityId, request.Request);
        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }

    private async Task<bool> DependenciesAllowActivationAsync(Guid tenantId, IReadOnlyCollection<Guid> scope, TepCandidateProfileMetadata entity, CancellationToken ct)
    {
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

        return CandidateProfileGuard.AssociationAllowsCandidate(entity, association)
            && CandidateProfileGuard.PolicyAllowsCandidate(policy)
            && CandidateProfileGuard.VerifiedAccessAllowsCandidate(entity, verified)
            && CandidateProfileGuard.ReviewBoardAllowsCandidate(entity, review)
            && CandidateProfileGuard.TrustLevelAllowsCandidate(entity, trust);
    }

    private static TepCandidateProfileMetadata ToEntity(Guid tenantId, Guid legalEntityId, CandidateProfileRequest request) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = request.Code.Trim(),
            DisplayName = request.DisplayName.Trim(),
            CandidateReference = request.CandidateReference.Trim(),
            TalentProfileReference = request.TalentProfileReference.Trim(),
            AssociationMembershipId = request.AssociationMembershipId,
            ConsentVisibilityPolicyId = request.ConsentVisibilityPolicyId,
            VerifiedParticipantId = request.VerifiedParticipantId,
            ReviewBoardCaseId = request.ReviewBoardCaseId,
            TrustLevelPolicyId = request.TrustLevelPolicyId,
            HcmFoundationReference = request.HcmFoundationReference.Trim(),
            SkillSummaryMetadata = request.SkillSummaryMetadata.Trim(),
            CredentialSummaryMetadata = request.CredentialSummaryMetadata.Trim(),
            ExperienceSummaryMetadata = request.ExperienceSummaryMetadata.Trim(),
            CandidateIdentityState = request.CandidateIdentityState,
            TalentProfileState = request.TalentProfileState,
            ProfileCompletenessState = request.ProfileCompletenessState,
            VisibilityClassification = request.VisibilityClassification,
            ConsentBasisState = request.ConsentBasisState,
            PolicyEvaluationState = request.PolicyEvaluationState,
            ProfilePolicyEvaluationState = request.ProfilePolicyEvaluationState,
            VisibilityApprovalState = request.VisibilityApprovalState,
            DataScopeState = request.DataScopeState,
            DataMinimizationState = request.DataMinimizationState,
            AssociationValidationState = request.AssociationValidationState,
            VerifiedAccessValidationState = request.VerifiedAccessValidationState,
            ReviewBoardValidationState = request.ReviewBoardValidationState,
            TrustLevelValidationState = request.TrustLevelValidationState,
            HcmValidationState = request.HcmValidationState,
            DependencyStates = request.DependencyStates.Select(CandidateProfileMapper.ToEntity).ToList(),
            SourceContractVersion = request.SourceContractVersion.Trim(),
            LastEvaluatedAt = request.LastEvaluatedAt,
            CandidateVersion = request.CandidateVersion,
            LocalAuditEvidenceRetentionState = request.LocalAuditEvidenceRetentionState,
            DeferredReason = string.IsNullOrWhiteSpace(request.DeferredReason) ? null : request.DeferredReason.Trim()
        };
}
