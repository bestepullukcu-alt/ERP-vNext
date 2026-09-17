using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Handlers;

public sealed class EvaluateConsentVisibilityPolicyHandler
    : IRequestHandler<EvaluateConsentVisibilityPolicyCommand, Response<ConsentVisibilityPolicyEvaluationDto>>
{
    private readonly ITepConsentVisibilityPolicyRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateConsentVisibilityPolicyHandler(ITepConsentVisibilityPolicyRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<ConsentVisibilityPolicyEvaluationDto>> Handle(EvaluateConsentVisibilityPolicyCommand request, CancellationToken ct)
    {
        var tenant = ConsentVisibilityPolicyGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ConsentVisibilityPolicyEvaluationDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<ConsentVisibilityPolicyEvaluationDto>.Fail("Consent/visibility policy was not found.", 404);
        }

        var result = ConsentVisibilityPolicyGuard.Evaluate(entity, request.Request.AssociationActivationRequested);
        entity.LastEvaluatedAt = result.EvaluatedAt;
        entity.UpdatedAt = result.EvaluatedAt;
        await _repository.UpdateAsync(entity, ct);

        return Response<ConsentVisibilityPolicyEvaluationDto>.Success(result);
    }
}
