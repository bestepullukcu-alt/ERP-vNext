using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Handlers;

public sealed class EvaluateReviewBoardCaseHandler : IRequestHandler<EvaluateReviewBoardCaseCommand, Response<ReviewBoardEvaluationDto>>
{
    private readonly ITepReviewBoardCaseMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedAccessRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateReviewBoardCaseHandler(
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

    public async Task<Response<ReviewBoardEvaluationDto>> Handle(EvaluateReviewBoardCaseCommand request, CancellationToken ct)
    {
        var tenant = ReviewBoardGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ReviewBoardEvaluationDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);

        var entity = await _repository.GetByIdAsync(tenantId, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<ReviewBoardEvaluationDto>.Fail("Review-board case was not found.", 404);
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

        var evaluation = ReviewBoardGuard.Evaluate(entity, association, policy, verifiedAccess, request.Request.ReviewRequested);
        if (request.Request.ReviewRequested && !evaluation.ReviewAllowed)
        {
            return Response<ReviewBoardEvaluationDto>.Fail("Review-board evaluation failed closed.", 404);
        }

        entity.ReviewBoardCaseState = evaluation.ReviewAllowed ? TepReviewBoardCaseState.UnderReview : TepReviewBoardCaseState.Deferred;
        entity.ReviewDecisionState = evaluation.ReviewAllowed ? TepReviewDecisionState.NotReviewed : TepReviewDecisionState.Deferred;
        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.DeferredReason = evaluation.EvaluationDeferred ? "Review-board dependency evaluation deferred." : entity.DeferredReason;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<ReviewBoardEvaluationDto>.Success(evaluation);
    }
}
