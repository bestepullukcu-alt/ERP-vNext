using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Handlers;

public sealed class UpdateConsentVisibilityPolicyHandler
    : IRequestHandler<UpdateConsentVisibilityPolicyCommand, Response<ConsentVisibilityPolicyDto>>
{
    private readonly ITepConsentVisibilityPolicyRepository _repository;
    private readonly ITenantContext _tenantContext;

    public UpdateConsentVisibilityPolicyHandler(ITepConsentVisibilityPolicyRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ConsentVisibilityPolicyDto>> Handle(UpdateConsentVisibilityPolicyCommand request, CancellationToken ct)
    {
        var tenant = ConsentVisibilityPolicyGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ConsentVisibilityPolicyDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<ConsentVisibilityPolicyDto>.Fail("Consent/visibility policy was not found.", 404);
        }

        var validation = ConsentVisibilityPolicyGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<ConsentVisibilityPolicyDto>.Fail(validation, 400);
        }

        var activation = ConsentVisibilityPolicyGuard.ValidateActivationRules(request.Request);
        if (!activation.IsSuccessful)
        {
            return Response<ConsentVisibilityPolicyDto>.Fail(activation.Errors, activation.StatusCode);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, request.Request.Code.Trim(), request.Id, ct))
        {
            return Response<ConsentVisibilityPolicyDto>.Fail("An active consent/visibility policy with the same Code already exists for this tenant.", 409);
        }

        entity.Code = request.Request.Code.Trim();
        entity.DisplayName = request.Request.DisplayName.Trim();
        entity.PolicyState = request.Request.PolicyState;
        entity.ConsentRequirementState = request.Request.ConsentRequirementState;
        entity.VisibilityScope = request.Request.VisibilityScope;
        entity.DataScopeState = request.Request.DataScopeState;
        entity.AccessPolicyState = request.Request.AccessPolicyState;
        entity.AssociationConsumptionState = request.Request.AssociationConsumptionState;
        entity.PolicyUnavailableBehavior = request.Request.PolicyUnavailableBehavior;
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.DependencyStates = request.Request.DependencyStates.Select(ConsentVisibilityPolicyMapper.ToEntity).ToList();
        entity.LocalAuditEvidenceRetentionState = request.Request.LocalAuditEvidenceRetentionState;
        entity.LastEvaluatedAt = request.Request.LastEvaluatedAt;
        entity.PolicyVersion = request.Request.PolicyVersion;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<ConsentVisibilityPolicyDto>.Success(ConsentVisibilityPolicyMapper.ToDto(entity));
    }
}
