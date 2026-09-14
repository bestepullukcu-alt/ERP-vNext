using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Handlers;

public sealed class UpdateVerifiedParticipantAccessHandler
    : IRequestHandler<UpdateVerifiedParticipantAccessCommand, Response<VerifiedParticipantAccessDto>>
{
    private readonly ITepVerifiedParticipantAccessRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public UpdateVerifiedParticipantAccessHandler(
        ITepVerifiedParticipantAccessRepository repository,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _associationRepository = associationRepository;
        _policyRepository = policyRepository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<VerifiedParticipantAccessDto>> Handle(UpdateVerifiedParticipantAccessCommand request, CancellationToken ct)
    {
        var tenant = VerifiedParticipantGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<VerifiedParticipantAccessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);

        var entity = await _repository.GetByIdAsync(tenantId, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<VerifiedParticipantAccessDto>.Fail("Verified participant access record was not found.", 404);
        }

        var validation = VerifiedParticipantGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<VerifiedParticipantAccessDto>.Fail(validation, 400);
        }

        var verification = VerifiedParticipantGuard.ValidateVerificationRequest(request.Request);
        if (!verification.IsSuccessful)
        {
            return Response<VerifiedParticipantAccessDto>.Fail(verification.Errors, verification.StatusCode);
        }

        if (VerifiedParticipantGuard.IsVerificationRequested(request.Request.VerificationState, request.Request.AccessState)
            && !await DependenciesAllowAccessAsync(tenantId, scope, request.Request, ct))
        {
            return Response<VerifiedParticipantAccessDto>.Fail("Verified participant access requires same-tenant Association and Consent/Visibility preconditions.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, entity.LegalEntityId, request.Request.Code.Trim(), request.Id, ct))
        {
            return Response<VerifiedParticipantAccessDto>.Fail("An active verified participant access record with the same Code already exists for this tenant.", 409);
        }

        entity.Code = request.Request.Code.Trim();
        entity.DisplayName = request.Request.DisplayName.Trim();
        entity.AssociationMembershipId = request.Request.AssociationMembershipId;
        entity.MemberCompanyReference = request.Request.MemberCompanyReference.Trim();
        entity.HrParticipantReference = request.Request.HrParticipantReference.Trim();
        entity.HcmFoundationReference = request.Request.HcmFoundationReference.Trim();
        entity.ConsentVisibilityPolicyId = request.Request.ConsentVisibilityPolicyId;
        entity.VerificationState = request.Request.VerificationState;
        entity.AccessState = request.Request.AccessState;
        entity.PolicyEvaluationState = request.Request.PolicyEvaluationState;
        entity.VisibilityApprovalState = request.Request.VisibilityApprovalState;
        entity.HcmValidationState = request.Request.HcmValidationState;
        entity.AssociationValidationState = request.Request.AssociationValidationState;
        entity.VerifiedCompanyAccessState = request.Request.VerifiedCompanyAccessState;
        entity.DependencyStates = request.Request.DependencyStates.Select(VerifiedParticipantMapper.ToEntity).ToList();
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.LastEvaluatedAt = request.Request.LastEvaluatedAt;
        entity.VerificationVersion = request.Request.VerificationVersion;
        entity.LocalAuditEvidenceRetentionState = request.Request.LocalAuditEvidenceRetentionState;
        entity.DeferredReason = string.IsNullOrWhiteSpace(request.Request.DeferredReason) ? null : request.Request.DeferredReason.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<VerifiedParticipantAccessDto>.Success(VerifiedParticipantMapper.ToDto(entity));
    }

    private async Task<bool> DependenciesAllowAccessAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, VerifiedParticipantAccessRequest request, CancellationToken ct)
    {
        var association = request.AssociationMembershipId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, legalEntityIds, associationId, ct)
            : null;
        var policy = request.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, legalEntityIds, policyId, ct)
            : null;

        return VerifiedParticipantGuard.AssociationAllowsAccess(new TepVerifiedParticipantAccess
            {
                AssociationMembershipId = request.AssociationMembershipId
            }, association)
            && VerifiedParticipantGuard.PolicyAllowsAccess(policy);
    }
}
