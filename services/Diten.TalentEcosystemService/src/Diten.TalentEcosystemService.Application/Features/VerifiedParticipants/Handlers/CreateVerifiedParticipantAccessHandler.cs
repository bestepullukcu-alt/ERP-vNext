using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Handlers;

public sealed class CreateVerifiedParticipantAccessHandler
    : IRequestHandler<CreateVerifiedParticipantAccessCommand, Response<Guid>>
{
    private readonly ITepVerifiedParticipantAccessRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateVerifiedParticipantAccessHandler(
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

    public async Task<Response<Guid>> Handle(CreateVerifiedParticipantAccessCommand request, CancellationToken ct)
    {
        var tenant = VerifiedParticipantGuard.RequireTenant(_tenantContext);
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

        var validation = VerifiedParticipantGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<Guid>.Fail(validation, 400);
        }

        var verification = VerifiedParticipantGuard.ValidateVerificationRequest(request.Request);
        if (!verification.IsSuccessful)
        {
            return Response<Guid>.Fail(verification.Errors, verification.StatusCode);
        }

        if (VerifiedParticipantGuard.IsVerificationRequested(request.Request.VerificationState, request.Request.AccessState)
            && !await DependenciesAllowAccessAsync(tenantId, scope, request.Request, ct))
        {
            return Response<Guid>.Fail("Verified participant access requires same-tenant Association and Consent/Visibility preconditions.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, request.Request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("An active verified participant access record with the same Code already exists for this tenant.", 409);
        }

        var entity = new TepVerifiedParticipantAccess
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = request.Request.Code.Trim(),
            DisplayName = request.Request.DisplayName.Trim(),
            AssociationMembershipId = request.Request.AssociationMembershipId,
            MemberCompanyReference = request.Request.MemberCompanyReference.Trim(),
            HrParticipantReference = request.Request.HrParticipantReference.Trim(),
            HcmFoundationReference = request.Request.HcmFoundationReference.Trim(),
            ConsentVisibilityPolicyId = request.Request.ConsentVisibilityPolicyId,
            VerificationState = request.Request.VerificationState,
            AccessState = request.Request.AccessState,
            PolicyEvaluationState = request.Request.PolicyEvaluationState,
            VisibilityApprovalState = request.Request.VisibilityApprovalState,
            HcmValidationState = request.Request.HcmValidationState,
            AssociationValidationState = request.Request.AssociationValidationState,
            VerifiedCompanyAccessState = request.Request.VerifiedCompanyAccessState,
            DependencyStates = request.Request.DependencyStates.Select(VerifiedParticipantMapper.ToEntity).ToList(),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            LastEvaluatedAt = request.Request.LastEvaluatedAt,
            VerificationVersion = request.Request.VerificationVersion,
            LocalAuditEvidenceRetentionState = request.Request.LocalAuditEvidenceRetentionState,
            DeferredReason = string.IsNullOrWhiteSpace(request.Request.DeferredReason) ? null : request.Request.DeferredReason.Trim()
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
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
