using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Handlers;

public sealed class CreateExitReferenceRecordHandler : IRequestHandler<CreateExitReferenceRecordCommand, Response<Guid>>
{
    private readonly ITepExitReferenceRecordMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedRepository;
    private readonly ITepReviewBoardCaseMetadataRepository _reviewRepository;
    private readonly ITepTrustLevelPolicyMetadataRepository _trustRepository;
    private readonly ITepCandidateProfileMetadataRepository _candidateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateExitReferenceRecordHandler(
        ITepExitReferenceRecordMetadataRepository repository,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITepVerifiedParticipantAccessRepository verifiedRepository,
        ITepReviewBoardCaseMetadataRepository reviewRepository,
        ITepTrustLevelPolicyMetadataRepository trustRepository,
        ITepCandidateProfileMetadataRepository candidateRepository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _associationRepository = associationRepository;
        _policyRepository = policyRepository;
        _verifiedRepository = verifiedRepository;
        _reviewRepository = reviewRepository;
        _trustRepository = trustRepository;
        _candidateRepository = candidateRepository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateExitReferenceRecordCommand request, CancellationToken ct)
    {
        var tenant = ExitReferenceRecordGuard.RequireTenant(_tenantContext);
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

        var validation = ExitReferenceRecordGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<Guid>.Fail(validation, 400);
        }

        var activation = ExitReferenceRecordGuard.ValidateActivationRequest(request.Request);
        if (!activation.IsSuccessful)
        {
            return Response<Guid>.Fail(activation.Errors, activation.StatusCode);
        }

        var entity = ExitReferenceRecordHandlerMapper.ToEntity(tenantId, request.Request);
        entity.LegalEntityId = legalEntityId;
        if (ExitReferenceRecordGuard.IsActivationRequested(request.Request.ReferenceRecordState)
            && !await DependenciesAllowActivationAsync(tenantId, scope, entity, ct))
        {
            return Response<Guid>.Fail("Exit reference record activation requires same-tenant dependency preconditions.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, request.Request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("An active exit reference record with the same Code already exists for this tenant.", 409);
        }

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }

    private async Task<bool> DependenciesAllowActivationAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, TepExitReferenceRecordMetadata entity, CancellationToken ct)
    {
        var dependencies = await ExitReferenceRecordDependencyReader.ReadAsync(
            tenantId,
            legalEntityIds,
            entity,
            _associationRepository,
            _policyRepository,
            _verifiedRepository,
            _reviewRepository,
            _trustRepository,
            _candidateRepository,
            ct);

        return ExitReferenceRecordGuard.AssociationAllowsReference(entity, dependencies.Association)
            && ExitReferenceRecordGuard.PolicyAllowsReference(dependencies.Policy)
            && ExitReferenceRecordGuard.VerifiedAccessAllowsReference(entity, dependencies.VerifiedAccess)
            && ExitReferenceRecordGuard.ReviewBoardAllowsReference(entity, dependencies.ReviewCase)
            && ExitReferenceRecordGuard.TrustLevelAllowsReference(entity, dependencies.TrustPolicy)
            && ExitReferenceRecordGuard.CandidateProfileAllowsReference(entity, dependencies.CandidateProfile);
    }
}
