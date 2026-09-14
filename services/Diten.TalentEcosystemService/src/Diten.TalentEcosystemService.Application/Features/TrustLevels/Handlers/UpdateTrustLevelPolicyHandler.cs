using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TrustLevels.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels.Handlers;

public sealed class UpdateTrustLevelPolicyHandler : IRequestHandler<UpdateTrustLevelPolicyCommand, Response<TrustLevelPolicyDto>>
{
    private readonly ITepTrustLevelPolicyMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedAccessRepository;
    private readonly ITepReviewBoardCaseMetadataRepository _reviewBoardRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public UpdateTrustLevelPolicyHandler(
        ITepTrustLevelPolicyMetadataRepository repository,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITepVerifiedParticipantAccessRepository verifiedAccessRepository,
        ITepReviewBoardCaseMetadataRepository reviewBoardRepository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _associationRepository = associationRepository;
        _policyRepository = policyRepository;
        _verifiedAccessRepository = verifiedAccessRepository;
        _reviewBoardRepository = reviewBoardRepository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<TrustLevelPolicyDto>> Handle(UpdateTrustLevelPolicyCommand request, CancellationToken ct)
    {
        var tenant = TrustLevelGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TrustLevelPolicyDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);

        var entity = await _repository.GetByIdAsync(tenantId, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<TrustLevelPolicyDto>.Fail("Trust-level policy was not found.", 404);
        }

        var validation = TrustLevelGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<TrustLevelPolicyDto>.Fail(validation, 400);
        }

        if (TrustLevelGuard.IsActivating(request.Request)
            && !await DependenciesAllowTrustAsync(tenantId, scope, request.Request, ct))
        {
            return Response<TrustLevelPolicyDto>.Fail("Trust activation requires same-tenant Association, Consent/Visibility, Verified Access, and Review Board preconditions.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, entity.LegalEntityId, request.Request.Code.Trim(), entity.Id, ct))
        {
            return Response<TrustLevelPolicyDto>.Fail("An active trust-level policy with the same Code already exists for this tenant.", 409);
        }

        entity.Code = request.Request.Code.Trim();
        entity.DisplayName = request.Request.DisplayName.Trim();
        entity.TrustLevelPolicyState = request.Request.TrustLevelPolicyState;
        entity.TrustValidationState = request.Request.TrustValidationState;
        entity.MultiSignatureRequirementState = request.Request.MultiSignatureRequirementState;
        entity.MultiSignaturePolicyUnavailableBehavior = request.Request.MultiSignaturePolicyUnavailableBehavior;
        entity.AssociationMembershipRegistryId = request.Request.AssociationMembershipRegistryId;
        entity.ConsentVisibilityPolicyId = request.Request.ConsentVisibilityPolicyId;
        entity.VerifiedParticipantAccessId = request.Request.VerifiedParticipantAccessId;
        entity.ReviewBoardCaseId = request.Request.ReviewBoardCaseId;
        entity.AssociationValidationState = request.Request.AssociationValidationState;
        entity.ConsentVisibilityValidationState = request.Request.ConsentVisibilityValidationState;
        entity.VerifiedAccessValidationState = request.Request.VerifiedAccessValidationState;
        entity.ReviewBoardValidationState = request.Request.ReviewBoardValidationState;
        entity.SignatureSubstrateState = request.Request.SignatureSubstrateState;
        entity.SignatureSubstrateReference = request.Request.SignatureSubstrateReference.Trim();
        entity.LegalSecurityTrustModelState = request.Request.LegalSecurityTrustModelState;
        entity.AuditEvidenceState = request.Request.AuditEvidenceState;
        entity.RetentionState = request.Request.RetentionState;
        entity.DependencyStates = request.Request.DependencyStates.Select(TrustLevelMapper.ToEntity).ToList();
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.LastEvaluatedAt = request.Request.LastEvaluatedAt;
        entity.TrustPolicyVersion = request.Request.TrustPolicyVersion;
        entity.DeferredReason = string.IsNullOrWhiteSpace(request.Request.DeferredReason) ? null : request.Request.DeferredReason.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<TrustLevelPolicyDto>.Success(TrustLevelMapper.ToDto(entity));
    }

    private async Task<bool> DependenciesAllowTrustAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, TrustLevelPolicyRequest request, CancellationToken ct)
    {
        var metadata = new TepTrustLevelPolicyMetadata
        {
            TenantId = tenantId,
            AssociationMembershipRegistryId = request.AssociationMembershipRegistryId,
            ConsentVisibilityPolicyId = request.ConsentVisibilityPolicyId,
            VerifiedParticipantAccessId = request.VerifiedParticipantAccessId,
            ReviewBoardCaseId = request.ReviewBoardCaseId,
            AssociationValidationState = request.AssociationValidationState,
            ConsentVisibilityValidationState = request.ConsentVisibilityValidationState,
            VerifiedAccessValidationState = request.VerifiedAccessValidationState,
            ReviewBoardValidationState = request.ReviewBoardValidationState,
            SignatureSubstrateState = request.SignatureSubstrateState,
            LegalSecurityTrustModelState = request.LegalSecurityTrustModelState,
            MultiSignatureRequirementState = request.MultiSignatureRequirementState,
            MultiSignaturePolicyUnavailableBehavior = request.MultiSignaturePolicyUnavailableBehavior
        };
        var association = request.AssociationMembershipRegistryId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, legalEntityIds, associationId, ct)
            : null;
        var policy = request.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, legalEntityIds, policyId, ct)
            : null;
        var verifiedAccess = request.VerifiedParticipantAccessId is { } verifiedAccessId
            ? await _verifiedAccessRepository.GetByIdAsync(tenantId, legalEntityIds, verifiedAccessId, ct)
            : null;
        var reviewBoard = request.ReviewBoardCaseId is { } reviewBoardId
            ? await _reviewBoardRepository.GetByIdAsync(tenantId, legalEntityIds, reviewBoardId, ct)
            : null;

        return TrustLevelGuard.AssociationAllowsTrust(metadata, association)
            && TrustLevelGuard.PolicyAllowsTrust(policy)
            && TrustLevelGuard.VerifiedAccessAllowsTrust(metadata, verifiedAccess)
            && TrustLevelGuard.ReviewBoardAllowsTrust(metadata, reviewBoard);
    }
}
