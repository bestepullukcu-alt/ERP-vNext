using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Handlers;

public sealed class ReviewReviewBoardCaseHandler : IRequestHandler<ReviewReviewBoardCaseCommand, Response<ReviewBoardCaseDto>>
{
    private readonly ITepReviewBoardCaseMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedAccessRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public ReviewReviewBoardCaseHandler(
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

    public async Task<Response<ReviewBoardCaseDto>> Handle(ReviewReviewBoardCaseCommand request, CancellationToken ct)
    {
        var tenant = ReviewBoardGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ReviewBoardCaseDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);

        var entity = await _repository.GetByIdAsync(tenantId, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<ReviewBoardCaseDto>.Fail("Review-board case was not found.", 404);
        }

        if (request.Request.ReviewDecisionState is TepReviewDecisionState.NotReviewed or TepReviewDecisionState.Deferred or TepReviewDecisionState.Archived)
        {
            return Response<ReviewBoardCaseDto>.Fail("Review decision must be Approved, Rejected, or Escalated.", 400);
        }

        var association = entity.AssociationMembershipRegistryId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, scope, associationId, ct)
            : null;
        var policy = entity.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, scope, policyId, ct)
            : null;
        var verifiedAccess = entity.VerifiedParticipantAccessId is { } verifiedAccessId
            ? await _verifiedAccessRepository.GetByIdAsync(tenantId, scope, verifiedAccessId, ct)
            : null;

        var evaluation = ReviewBoardGuard.Evaluate(entity, association, policy, verifiedAccess, true);
        if (!evaluation.ReviewAllowed)
        {
            return Response<ReviewBoardCaseDto>.Fail("Review-board decision requires approved preconditions and eligible reviewer metadata.", 404);
        }

        entity.ReviewBoardCaseState = TepReviewBoardCaseState.DecisionRecorded;
        entity.ReviewDecisionState = request.Request.ReviewDecisionState;
        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<ReviewBoardCaseDto>.Success(ReviewBoardMapper.ToDto(entity));
    }
}
