using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Handlers;

public sealed class UpdateAssociationMembershipHandler
    : IRequestHandler<UpdateAssociationMembershipCommand, Response<AssociationMembershipRegistryDto>>
{
    private readonly ITepAssociationMembershipRegistryRepository _repository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public UpdateAssociationMembershipHandler(
        ITepAssociationMembershipRegistryRepository repository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _policyRepository = policyRepository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<AssociationMembershipRegistryDto>> Handle(UpdateAssociationMembershipCommand request, CancellationToken ct)
    {
        var tenant = AssociationMembershipGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<AssociationMembershipRegistryDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;
        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);

        var entity = await _repository.GetByIdAsync(tenantId, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<AssociationMembershipRegistryDto>.Fail("Association membership registry record was not found.", 404);
        }

        var validation = AssociationMembershipGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<AssociationMembershipRegistryDto>.Fail(validation, 400);
        }

        var activation = AssociationMembershipGuard.ValidateActivationRequest(request.Request);
        if (!activation.IsSuccessful)
        {
            return Response<AssociationMembershipRegistryDto>.Fail(activation.Errors, activation.StatusCode);
        }

        if (AssociationMembershipGuard.IsActivationRequested(
                request.Request.AssociationMembershipState,
                request.Request.AssociationActivationState)
            && !await PolicyAllowsActivationAsync(tenantId, scope, request.Request.ConsentVisibilityPolicyId, ct))
        {
            return Response<AssociationMembershipRegistryDto>.Fail("Association activation requires an active same-tenant consent/visibility policy precondition.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, entity.LegalEntityId, request.Request.Code.Trim(), request.Id, ct))
        {
            return Response<AssociationMembershipRegistryDto>.Fail("An active association membership registry record with the same Code already exists for this tenant.", 409);
        }

        entity.Code = request.Request.Code.Trim();
        entity.DisplayName = request.Request.DisplayName.Trim();
        entity.AssociationMembershipState = request.Request.AssociationMembershipState;
        entity.MemberCompanyState = request.Request.MemberCompanyState;
        entity.MemberCompanyReference = request.Request.MemberCompanyReference.Trim();
        entity.HcmFoundationReference = request.Request.HcmFoundationReference.Trim();
        entity.ConsentVisibilityPolicyId = request.Request.ConsentVisibilityPolicyId;
        entity.PolicyEvaluationState = request.Request.PolicyEvaluationState;
        entity.VisibilityApprovalState = request.Request.VisibilityApprovalState;
        entity.AssociationActivationState = request.Request.AssociationActivationState;
        entity.DependencyStates = request.Request.DependencyStates.Select(AssociationMembershipMapper.ToEntity).ToList();
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.LastEvaluatedAt = request.Request.LastEvaluatedAt;
        entity.RegistryVersion = request.Request.RegistryVersion;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<AssociationMembershipRegistryDto>.Success(AssociationMembershipMapper.ToDto(entity));
    }

    private async Task<bool> PolicyAllowsActivationAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid? policyId, CancellationToken ct)
    {
        if (policyId is null)
        {
            return false;
        }

        var policy = await _policyRepository.GetByIdAsync(tenantId, legalEntityIds, policyId.Value, ct);
        return policy is not null
            && policy.PolicyState is TepPolicyState.Active
            && policy.ConsentRequirementState is TepConsentRequirementState.Approved
            && policy.VisibilityScope is TepVisibilityScope.AssociationVisible
            && policy.DataScopeState is TepDataScopeState.Available
            && policy.AccessPolicyState is TepAccessPolicyState.Approved
            && policy.PolicyUnavailableBehavior is TepPolicyUnavailableBehavior.FailClosed;
    }
}
