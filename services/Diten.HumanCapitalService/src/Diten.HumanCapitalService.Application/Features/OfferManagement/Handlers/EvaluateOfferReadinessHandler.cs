using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.OfferManagement.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OfferManagement.Handlers;

public sealed class EvaluateOfferReadinessHandler : IRequestHandler<EvaluateOfferReadinessCommand, Response<OfferReadinessDto>>
{
    private readonly IOfferReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateOfferReadinessHandler(IOfferReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<OfferReadinessDto>> Handle(EvaluateOfferReadinessCommand request, CancellationToken ct)
    {
        var tenant = OfferManagementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<OfferReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<OfferReadinessDto>.Fail("Offer readiness record was not found.", 404);
        }

        OfferManagementGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<OfferReadinessDto>.Success(OfferManagementMapper.ToDto(entity));
    }
}
