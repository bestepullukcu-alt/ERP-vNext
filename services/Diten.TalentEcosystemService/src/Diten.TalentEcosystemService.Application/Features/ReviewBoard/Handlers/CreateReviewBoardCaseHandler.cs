using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Handlers;

public sealed class CreateReviewBoardCaseHandler : IRequestHandler<CreateReviewBoardCaseCommand, Response<Guid>>
{
    private readonly ITepReviewBoardCaseMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedAccessRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateReviewBoardCaseHandler(
        ITepReviewBoardCaseMetadataRepository repository,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITepVerifiedParticipantAccessRepository verifiedAccessRepository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _associationRepository = associationRepository;
        _policyRepository = policyRepository;
        _verifiedAccessRepository = verifiedAccessRepository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateReviewBoardCaseCommand request, CancellationToken ct)
    {
        var tenant = ReviewBoardGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        if (!await _legalEntityContext.IsSelectionAllowedAsync(ct))
        {
            return Response<Guid>.Fail(
                "A permitted legal entity must be selected (X-Legal-Entity-Id) to create this record.",
                403);
        }

        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);

        var validation = ReviewBoardGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<Guid>.Fail(validation, 400);
        }

        var decisionValidation = ReviewBoardGuard.ValidateReviewDecisionRequest(request.Request);
        if (!decisionValidation.IsSuccessful)
        {
            return Response<Guid>.Fail(decisionValidation.Errors, decisionValidation.StatusCode);
        }

        if (ReviewBoardGuard.IsDecisionRequested(request.Request.ReviewBoardCaseState, request.Request.ReviewDecisionState)
            && !await DependenciesAllowReviewAsync(tenantId, scope, request.Request, ct))
        {
            return Response<Guid>.Fail("Review-board decision requires same-tenant Association, Consent/Visibility, and Verified Access preconditions.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, request.Request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("An active review-board case with the same Code already exists for this tenant.", 409);
        }

        var entity = new TepReviewBoardCaseMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = request.Request.Code.Trim(),
            DisplayName = request.Request.DisplayName.Trim(),
            ReviewBoardCaseState = request.Request.ReviewBoardCaseState,
            ReviewDecisionState = request.Request.ReviewDecisionState,
            AssociationMembershipRegistryId = request.Request.AssociationMembershipRegistryId,
            ConsentVisibilityPolicyId = request.Request.ConsentVisibilityPolicyId,
            VerifiedParticipantAccessId = request.Request.VerifiedParticipantAccessId,
            ReviewerEligibilityState = request.Request.ReviewerEligibilityState,
            SegregationOfDutiesState = request.Request.SegregationOfDutiesState,
            LegalSecurityDecisionState = request.Request.LegalSecurityDecisionState,
            ExternalReviewBoardState = request.Request.ExternalReviewBoardState,
            AuditEvidenceState = request.Request.AuditEvidenceState,
            RetentionState = request.Request.RetentionState,
            DependencyStates = request.Request.DependencyStates.Select(ReviewBoardMapper.ToEntity).ToList(),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            LastEvaluatedAt = request.Request.LastEvaluatedAt,
            ReviewBoardVersion = request.Request.ReviewBoardVersion,
            DeferredReason = string.IsNullOrWhiteSpace(request.Request.DeferredReason) ? null : request.Request.DeferredReason.Trim()
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }

    private async Task<bool> DependenciesAllowReviewAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, ReviewBoardCaseRequest request, CancellationToken ct)
    {
        var association = request.AssociationMembershipRegistryId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, legalEntityIds, associationId, ct)
            : null;
        var policy = request.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, legalEntityIds, policyId, ct)
            : null;
        var verifiedAccess = request.VerifiedParticipantAccessId is { } verifiedAccessId
            ? await _verifiedAccessRepository.GetByIdAsync(tenantId, legalEntityIds, verifiedAccessId, ct)
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
