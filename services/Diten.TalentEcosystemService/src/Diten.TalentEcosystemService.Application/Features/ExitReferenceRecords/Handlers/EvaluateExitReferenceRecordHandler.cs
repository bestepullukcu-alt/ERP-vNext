using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Handlers;

public sealed class EvaluateExitReferenceRecordHandler
    : IRequestHandler<EvaluateExitReferenceRecordCommand, Response<ExitReferenceEvaluationDto>>
{
    private readonly ITepExitReferenceRecordMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedRepository;
    private readonly ITepReviewBoardCaseMetadataRepository _reviewRepository;
    private readonly ITepTrustLevelPolicyMetadataRepository _trustRepository;
    private readonly ITepCandidateProfileMetadataRepository _candidateRepository;
    private readonly ITenantContext _tenantContext;

    public EvaluateExitReferenceRecordHandler(
        ITepExitReferenceRecordMetadataRepository repository,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITepVerifiedParticipantAccessRepository verifiedRepository,
        ITepReviewBoardCaseMetadataRepository reviewRepository,
        ITepTrustLevelPolicyMetadataRepository trustRepository,
        ITepCandidateProfileMetadataRepository candidateRepository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _associationRepository = associationRepository;
        _policyRepository = policyRepository;
        _verifiedRepository = verifiedRepository;
        _reviewRepository = reviewRepository;
        _trustRepository = trustRepository;
        _candidateRepository = candidateRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ExitReferenceEvaluationDto>> Handle(EvaluateExitReferenceRecordCommand request, CancellationToken ct)
    {
        var tenant = ExitReferenceRecordGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ExitReferenceEvaluationDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<ExitReferenceEvaluationDto>.Fail("Exit reference record was not found.", 404);
        }

        var dependencies = await ExitReferenceRecordDependencyReader.ReadAsync(
            tenantId,
            entity,
            _associationRepository,
            _policyRepository,
            _verifiedRepository,
            _reviewRepository,
            _trustRepository,
            _candidateRepository,
            ct);

        var evaluation = ExitReferenceRecordGuard.Evaluate(
            entity,
            dependencies.Association,
            dependencies.Policy,
            dependencies.VerifiedAccess,
            dependencies.ReviewCase,
            dependencies.TrustPolicy,
            dependencies.CandidateProfile,
            request.Request.ActivationRequested);

        if (request.Request.ActivationRequested && !evaluation.ActivationAllowed)
        {
            return Response<ExitReferenceEvaluationDto>.Fail("Exit reference record evaluation failed closed.", 404);
        }

        entity.ReferenceRecordState = evaluation.ActivationAllowed
            ? TepExitReferenceRecordState.Active
            : TepExitReferenceRecordState.Deferred;
        entity.ReferenceSharingState = evaluation.ActivationAllowed
            ? TepReferenceSharingState.LocalMetadata
            : TepReferenceSharingState.Deferred;
        entity.ConsentPreconditionState = evaluation.ActivationAllowed
            ? TepConsentRequirementState.Approved
            : TepConsentRequirementState.Deferred;
        entity.VisibilityApprovalState = evaluation.ActivationAllowed
            ? TepVisibilityApprovalState.Approved
            : TepVisibilityApprovalState.Deferred;
        entity.DataScopeState = evaluation.ActivationAllowed
            ? TepDataScopeState.Available
            : TepDataScopeState.Deferred;
        entity.EvidenceRetentionState = evaluation.ActivationAllowed
            ? TepEvidenceRetentionDecisionState.LocalMetadata
            : TepEvidenceRetentionDecisionState.Deferred;
        entity.ReviewDisputeBoundaryState = evaluation.ActivationAllowed
            ? TepReviewDisputeBoundaryState.PreconditionSatisfied
            : TepReviewDisputeBoundaryState.Deferred;
        entity.DependencyStates = ExitReferenceRecordGuard.BuildRequiredDependencyStates(
            evaluation.ActivationAllowed ? TepShellDependencyStatus.Available : TepShellDependencyStatus.Deferred,
            evaluation.EvaluationDeferred ? "Exit reference record dependency evaluation deferred." : null).ToList();
        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.DeferredReason = evaluation.EvaluationDeferred ? "Exit reference record dependency evaluation deferred." : entity.DeferredReason;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<ExitReferenceEvaluationDto>.Success(evaluation);
    }
}
