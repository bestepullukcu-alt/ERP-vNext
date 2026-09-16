using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Handlers;

public sealed class VerifyParticipantAccessHandler
    : IRequestHandler<VerifyParticipantAccessCommand, Response<VerifiedParticipantAccessDto>>
{
    private readonly ITepVerifiedParticipantAccessRepository _repository;
    private readonly ITepAssociationMembershipRegistryRepository _associationRepository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public VerifyParticipantAccessHandler(
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

    public async Task<Response<VerifiedParticipantAccessDto>> Handle(VerifyParticipantAccessCommand request, CancellationToken ct)
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

        var association = entity.AssociationMembershipId is { } associationId
            ? await _associationRepository.GetByIdAsync(tenantId, scope, associationId, ct)
            : null;
        var policy = entity.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenantId, scope, policyId, ct)
            : null;
        var evaluation = VerifiedParticipantGuard.Evaluate(entity, association, policy, request.Request.VerificationRequested);

        if (!request.Request.VerificationRequested || !evaluation.VerificationAllowed)
        {
            return Response<VerifiedParticipantAccessDto>.Fail("Verified participant access requires approved Association and Consent/Visibility preconditions.", 404);
        }

        entity.VerificationState = TepVerificationState.Verified;
        entity.AccessState = TepAccessState.Active;
        entity.PolicyEvaluationState = TepPolicyEvaluationState.Approved;
        entity.VisibilityApprovalState = TepVisibilityApprovalState.Approved;
        entity.AssociationValidationState = TepShellDependencyStatus.Available;
        entity.VerifiedCompanyAccessState = TepVerifiedCompanyAccessState.Ready;
        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<VerifiedParticipantAccessDto>.Success(VerifiedParticipantMapper.ToDto(entity));
    }
}
