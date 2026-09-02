using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Handlers;

public sealed class UpdateReviewBoardCaseHandler : IRequestHandler<UpdateReviewBoardCaseCommand, Response<ReviewBoardCaseDto>>
{
    private readonly ITepReviewBoardCaseMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedAccessRepository;
    private readonly ITenantContext _tenantContext;

    public UpdateReviewBoardCaseHandler(
        ITepReviewBoardCaseMetadataRepository repository,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITepVerifiedParticipantAccessRepository verifiedAccessRepository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _associationRepository = associationRepository;
        _policyRepository = policyRepository;
        _verifiedAccessRepository = verifiedAccessRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ReviewBoardCaseDto>> Handle(UpdateReviewBoardCaseCommand request, CancellationToken ct)
    {
        var tenant = ReviewBoardGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ReviewBoardCaseDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<ReviewBoardCaseDto>.Fail("Review-board case was not found.", 404);
        }

        var validation = ReviewBoardGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<ReviewBoardCaseDto>.Fail(validation, 400);
        }

        var decisionValidation = ReviewBoardGuard.ValidateReviewDecisionRequest(request.Request);
        if (!decisionValidation.IsSuccessful)
        {
            return Response<ReviewBoardCaseDto>.Fail(decisionValidation.Errors, decisionValidation.StatusCode);
        }

        if (ReviewBoardGuard.IsDecisionRequested(request.Request.ReviewBoardCaseState, request.Request.ReviewDecisionState)
            && !await DependenciesAllowReviewAsync(tenantId, request.Request, ct))
        {
            return Response<ReviewBoardCaseDto>.Fail("Review-board decision requires same-tenant Association, Consent/Visibility, and Verified Access preconditions.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, request.Request.Code.Trim(), request.Id, ct))
        {
            return Response<ReviewBoardCaseDto>.Fail("An active review-board case with the same Code already exists for this tenant.", 409);
        }

        entity.Code = request.Request.Code.Trim();
        entity.DisplayName = request.Request.DisplayName.Trim();
        entity.ReviewBoardCaseState = request.Request.ReviewBoardCaseState;
        entity.ReviewDecisionState = request.Request.ReviewDecisionState;
        entity.AssociationMembershipRegistryId = request.Request.AssociationMembershipRegistryId;
        entity.ConsentVisibilityPolicyId = request.Request.ConsentVisibilityPolicyId;
        entity.VerifiedParticipantAccessId = request.Request.VerifiedParticipantAccessId;
        entity.ReviewerEligibilityState = request.Request.ReviewerEligibilityState;
        entity.SegregationOfDutiesState = request.Request.SegregationOfDutiesState;
        entity.LegalSecurityDecisionState = request.Request.LegalSecurityDecisionState;
        entity.ExternalReviewBoardState = request.Request.ExternalReviewBoardState;
        entity.AuditEvidenceState = request.Request.AuditEvidenceState;
        entity.RetentionState = request.Request.RetentionState;
        entity.DependencyStates = request.Request.DependencyStates.Select(ReviewBoardMapper.ToEntity).ToList();
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.LastEvaluatedAt = request.Request.LastEvaluatedAt;
        entity.ReviewBoardVersion = request.Request.ReviewBoardVersion;
        entity.DeferredReason = string.IsNullOrWhiteSpace(request.Request.DeferredReason) ? null : request.Request.DeferredReason.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<ReviewBoardCaseDto>.Success(ReviewBoardMapper.ToDto(entity));
    }

    private async Task<bool> DependenciesAllowReviewAsync(Guid tenantId, ReviewBoardCaseRequest request, CancellationToken ct)
    {
        var association = request.AssociationMembershipRegistryId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, associationId, ct)
            : null;
        var policy = request.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, policyId, ct)
            : null;
        var verifiedAccess = request.VerifiedParticipantAccessId is { } verifiedAccessId
            ? await _verifiedAccessRepository.GetByIdAsync(tenantId, verifiedAccessId, ct)
            : null;

        return ReviewBoardGuard.AssociationAllowsReview(new TepReviewBoardCaseMetadata
            {
                AssociationMembershipRegistryId = request.AssociationMembershipRegistryId
            }, association)
            && ReviewBoardGuard.PolicyAllowsReview(policy)
            && ReviewBoardGuard.VerifiedAccessAllowsReview(new TepReviewBoardCaseMetadata
            {
                VerifiedParticipantAccessId = request.VerifiedParticipantAccessId
            }, verifiedAccess);
    }
}
