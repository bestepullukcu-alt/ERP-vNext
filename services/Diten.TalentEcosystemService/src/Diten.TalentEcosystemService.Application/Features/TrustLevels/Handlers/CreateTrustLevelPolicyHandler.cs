using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TrustLevels.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels.Handlers;

public sealed class CreateTrustLevelPolicyHandler : IRequestHandler<CreateTrustLevelPolicyCommand, Response<Guid>>
{
    private readonly ITepTrustLevelPolicyMetadataRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITepVerifiedParticipantAccessRepository _verifiedAccessRepository;
    private readonly ITepReviewBoardCaseMetadataRepository _reviewBoardRepository;
    private readonly ITenantContext _tenantContext;

    public CreateTrustLevelPolicyHandler(
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

    public async Task<Response<Guid>> Handle(CreateTrustLevelPolicyCommand request, CancellationToken ct)
    {
        var tenant = TrustLevelGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        var validation = TrustLevelGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<Guid>.Fail(validation, 400);
        }

        if (TrustLevelGuard.IsActivating(request.Request)
            && !await DependenciesAllowTrustAsync(tenantId, request.Request, ct))
        {
            return Response<Guid>.Fail("Trust activation requires same-tenant Association, Consent/Visibility, Verified Access, and Review Board preconditions.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, request.Request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("An active trust-level policy with the same Code already exists for this tenant.", 409);
        }

        var entity = ToEntity(request.Request, tenantId);
        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }

    private async Task<bool> DependenciesAllowTrustAsync(Guid tenantId, TrustLevelPolicyRequest request, CancellationToken ct)
    {
        var metadata = ToEntity(request, tenantId);
        var association = request.AssociationMembershipRegistryId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, associationId, ct)
            : null;
        var policy = request.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, policyId, ct)
            : null;
        var verifiedAccess = request.VerifiedParticipantAccessId is { } verifiedAccessId
            ? await _verifiedAccessRepository.GetByIdAsync(tenantId, verifiedAccessId, ct)
            : null;
        var reviewBoard = request.ReviewBoardCaseId is { } reviewBoardId
            ? await _reviewBoardRepository.GetByIdAsync(tenantId, reviewBoardId, ct)
            : null;

        return TrustLevelGuard.AssociationAllowsTrust(metadata, association)
            && TrustLevelGuard.PolicyAllowsTrust(policy)
            && TrustLevelGuard.VerifiedAccessAllowsTrust(metadata, verifiedAccess)
            && TrustLevelGuard.ReviewBoardAllowsTrust(metadata, reviewBoard);
    }

    private static TepTrustLevelPolicyMetadata ToEntity(TrustLevelPolicyRequest request, Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Code = request.Code.Trim(),
            DisplayName = request.DisplayName.Trim(),
            TrustLevelPolicyState = request.TrustLevelPolicyState,
            TrustValidationState = request.TrustValidationState,
            MultiSignatureRequirementState = request.MultiSignatureRequirementState,
            MultiSignaturePolicyUnavailableBehavior = request.MultiSignaturePolicyUnavailableBehavior,
            AssociationMembershipRegistryId = request.AssociationMembershipRegistryId,
            ConsentVisibilityPolicyId = request.ConsentVisibilityPolicyId,
            VerifiedParticipantAccessId = request.VerifiedParticipantAccessId,
            ReviewBoardCaseId = request.ReviewBoardCaseId,
            AssociationValidationState = request.AssociationValidationState,
            ConsentVisibilityValidationState = request.ConsentVisibilityValidationState,
            VerifiedAccessValidationState = request.VerifiedAccessValidationState,
            ReviewBoardValidationState = request.ReviewBoardValidationState,
            SignatureSubstrateState = request.SignatureSubstrateState,
            SignatureSubstrateReference = request.SignatureSubstrateReference.Trim(),
            LegalSecurityTrustModelState = request.LegalSecurityTrustModelState,
            AuditEvidenceState = request.AuditEvidenceState,
            RetentionState = request.RetentionState,
            DependencyStates = request.DependencyStates.Select(TrustLevelMapper.ToEntity).ToList(),
            SourceContractVersion = request.SourceContractVersion.Trim(),
            LastEvaluatedAt = request.LastEvaluatedAt,
            TrustPolicyVersion = request.TrustPolicyVersion,
            DeferredReason = string.IsNullOrWhiteSpace(request.DeferredReason) ? null : request.DeferredReason.Trim()
        };
}
