using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Handlers;

public sealed class EvaluateAssociationMembershipHandler
    : IRequestHandler<EvaluateAssociationMembershipCommand, Response<AssociationMembershipEvaluationDto>>
{
    private readonly ITepAssociationMembershipRegistryRepository _repository;
    private readonly ITepConsentVisibilityPolicyRepository _policyRepository;
    private readonly ITenantContext _tenantContext;

    public EvaluateAssociationMembershipHandler(
        ITepAssociationMembershipRegistryRepository repository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _policyRepository = policyRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<AssociationMembershipEvaluationDto>> Handle(EvaluateAssociationMembershipCommand request, CancellationToken ct)
    {
        var tenant = AssociationMembershipGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<AssociationMembershipEvaluationDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<AssociationMembershipEvaluationDto>.Fail("Association membership registry record was not found.", 404);
        }

        var policy = entity.ConsentVisibilityPolicyId is { } policyId
            ? await _policyRepository.GetByIdAsync(tenant.Data, policyId, ct)
            : null;
        var evaluation = AssociationMembershipGuard.Evaluate(entity, policy, request.Request.ActivationRequested);

        if (request.Request.ActivationRequested && !evaluation.ActivationAllowed)
        {
            entity.PolicyEvaluationState = TepPolicyEvaluationState.Rejected;
            entity.AssociationActivationState = TepAssociationActivationState.Deferred;
            entity.LastEvaluatedAt = evaluation.EvaluatedAt;
            entity.UpdatedAt = evaluation.EvaluatedAt;
            await _repository.UpdateAsync(entity, ct);
            return Response<AssociationMembershipEvaluationDto>.Fail("Association activation requires approved same-tenant policy and visibility preconditions.", 404);
        }

        if (evaluation.ActivationAllowed)
        {
            entity.PolicyEvaluationState = TepPolicyEvaluationState.Approved;
            entity.VisibilityApprovalState = TepVisibilityApprovalState.Approved;
            entity.AssociationActivationState = TepAssociationActivationState.Ready;
        }
        else if (evaluation.EvaluationDeferred)
        {
            entity.PolicyEvaluationState = TepPolicyEvaluationState.Deferred;
            entity.AssociationActivationState = TepAssociationActivationState.Deferred;
        }

        entity.LastEvaluatedAt = evaluation.EvaluatedAt;
        entity.UpdatedAt = evaluation.EvaluatedAt;
        await _repository.UpdateAsync(entity, ct);

        return Response<AssociationMembershipEvaluationDto>.Success(evaluation);
    }
}
