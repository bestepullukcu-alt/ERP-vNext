using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Handlers;

public sealed class CreateAssociationMembershipHandler
    : IRequestHandler<CreateAssociationMembershipCommand, Response<Guid>>
{
    private readonly ITepAssociationMembershipRegistryRepository _repository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateAssociationMembershipHandler(
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

    public async Task<Response<Guid>> Handle(CreateAssociationMembershipCommand request, CancellationToken ct)
    {
        var tenant = AssociationMembershipGuard.RequireTenant(_tenantContext);
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

        var validation = AssociationMembershipGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<Guid>.Fail(validation, 400);
        }

        var activation = AssociationMembershipGuard.ValidateActivationRequest(request.Request);
        if (!activation.IsSuccessful)
        {
            return Response<Guid>.Fail(activation.Errors, activation.StatusCode);
        }

        if (AssociationMembershipGuard.IsActivationRequested(
                request.Request.AssociationMembershipState,
                request.Request.AssociationActivationState)
            && !await PolicyAllowsActivationAsync(tenantId, scope, request.Request.ConsentVisibilityPolicyId, ct))
        {
            return Response<Guid>.Fail("Association activation requires an active same-tenant consent/visibility policy precondition.", 404);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, request.Request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("An active association membership registry record with the same Code already exists for this tenant.", 409);
        }

        var entity = new TepAssociationMembershipRegistry
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = request.Request.Code.Trim(),
            DisplayName = request.Request.DisplayName.Trim(),
            AssociationMembershipState = request.Request.AssociationMembershipState,
            MemberCompanyState = request.Request.MemberCompanyState,
            MemberCompanyReference = request.Request.MemberCompanyReference.Trim(),
            HcmFoundationReference = request.Request.HcmFoundationReference.Trim(),
            ConsentVisibilityPolicyId = request.Request.ConsentVisibilityPolicyId,
            PolicyEvaluationState = request.Request.PolicyEvaluationState,
            VisibilityApprovalState = request.Request.VisibilityApprovalState,
            AssociationActivationState = request.Request.AssociationActivationState,
            DependencyStates = request.Request.DependencyStates.Select(AssociationMembershipMapper.ToEntity).ToList(),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            LastEvaluatedAt = request.Request.LastEvaluatedAt,
            RegistryVersion = request.Request.RegistryVersion
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }

    private async Task<bool> PolicyAllowsActivationAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid? policyId, CancellationToken ct)
    {
        if (policyId is null)
        {
            return false;
        }

        var policy = await _policyRepository.GetByIdAsync(tenantId, legalEntityIds, policyId.Value, ct);
        return policy is not null
            && AssociationMembershipGuard.Evaluate(new TepAssociationMembershipRegistry
            {
                Id = Guid.NewGuid(),
                ConsentVisibilityPolicyId = policyId,
                PolicyEvaluationState = TepPolicyEvaluationState.Approved,
                VisibilityApprovalState = TepVisibilityApprovalState.Approved,
                MemberCompanyState = TepMemberCompanyState.Verified
            }, policy, true).ActivationAllowed;
    }
}
