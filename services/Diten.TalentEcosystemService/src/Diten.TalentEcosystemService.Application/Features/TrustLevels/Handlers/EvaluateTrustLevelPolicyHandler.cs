using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TrustLevels.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels.Handlers;

public sealed class EvaluateTrustLevelPolicyHandler : IRequestHandler<EvaluateTrustLevelPolicyCommand, Response<TrustLevelEvaluationDto>>
{
    private readonly ITepTrustLevelPolicyMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedAccessRepository;
    private readonly ITepReviewBoardCaseMetadataRepository _reviewBoardRepository;
    private readonly ITenantContext _tenantContext;

    public EvaluateTrustLevelPolicyHandler(
        ITepTrustLevelPolicyMetadataRepository repository,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITepVerifiedParticipantAccessRepository verifiedAccessRepository,
        ITepReviewBoardCaseMetadataRepository reviewBoardRepository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _associationRepository = associationRepository;
        _policyRepository = policyRepository;
        _verifiedAccessRepository = verifiedAccessRepository;
        _reviewBoardRepository = reviewBoardRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TrustLevelEvaluationDto>> Handle(EvaluateTrustLevelPolicyCommand request, CancellationToken ct)
    {
        var tenant = TrustLevelGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TrustLevelEvaluationDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<TrustLevelEvaluationDto>.Fail("Trust-level policy was not found.", 404);
        }

        var association = entity.AssociationMembershipRegistryId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, associationId, ct)
            : null;
        var policy = entity.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, policyId, ct)
            : null;
        var verifiedAccess = entity.VerifiedParticipantAccessId is { } verifiedAccessId
            ? await _verifiedAccessRepository.GetByIdAsync(tenantId, verifiedAccessId, ct)
            : null;
        var reviewBoard = entity.ReviewBoardCaseId is { } reviewBoardId
            ? await _reviewBoardRepository.GetByIdAsync(tenantId, reviewBoardId, ct)
            : null;

        var evaluation = TrustLevelGuard.Evaluate(entity, association, policy, verifiedAccess, reviewBoard, request.Request.TrustElevationRequested);
        if (request.Request.TrustElevationRequested && !evaluation.TrustElevationAllowed)
        {
            return Response<TrustLevelEvaluationDto>.Fail("Trust-level evaluation failed closed.", 404);
        }

        entity.TrustValidationState = evaluation.TrustElevationAllowed ? TepTrustValidationState.Approved : TepTrustValidationState.Deferred;
        entity.TrustLevelPolicyState = evaluation.TrustElevationAllowed ? TepTrustLevelPolicyState.Active : TepTrustLevelPolicyState.Deferred;
        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.DeferredReason = evaluation.EvaluationDeferred ? "Trust-level dependency evaluation deferred." : entity.DeferredReason;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<TrustLevelEvaluationDto>.Success(evaluation);
    }
}
