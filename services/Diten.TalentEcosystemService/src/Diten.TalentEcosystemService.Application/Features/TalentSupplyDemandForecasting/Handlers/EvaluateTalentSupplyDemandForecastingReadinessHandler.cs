using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Handlers;

public sealed class EvaluateTalentSupplyDemandForecastingReadinessHandler : IRequestHandler<EvaluateTalentSupplyDemandForecastingReadinessCommand, Response<TalentSupplyDemandForecastingReadinessDto>>
{
    private readonly ITalentSupplyDemandForecastingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateTalentSupplyDemandForecastingReadinessHandler(ITalentSupplyDemandForecastingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<TalentSupplyDemandForecastingReadinessDto>> Handle(EvaluateTalentSupplyDemandForecastingReadinessCommand request, CancellationToken ct)
    {
        var tenant = TalentSupplyDemandForecastingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TalentSupplyDemandForecastingReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<TalentSupplyDemandForecastingReadinessDto>.Fail("TalentSupplyDemandForecasting readiness record was not found.", 404);
        }

        TalentSupplyDemandForecastingGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<TalentSupplyDemandForecastingReadinessDto>.Success(TalentSupplyDemandForecastingMapper.ToDto(entity));
    }
}
