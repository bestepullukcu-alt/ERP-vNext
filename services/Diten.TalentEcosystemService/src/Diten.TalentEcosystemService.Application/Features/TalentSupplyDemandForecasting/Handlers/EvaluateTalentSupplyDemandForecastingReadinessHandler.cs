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

    public EvaluateTalentSupplyDemandForecastingReadinessHandler(ITalentSupplyDemandForecastingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TalentSupplyDemandForecastingReadinessDto>> Handle(EvaluateTalentSupplyDemandForecastingReadinessCommand request, CancellationToken ct)
    {
        var tenant = TalentSupplyDemandForecastingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TalentSupplyDemandForecastingReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<TalentSupplyDemandForecastingReadinessDto>.Fail("TalentSupplyDemandForecasting readiness record was not found.", 404);
        }

        TalentSupplyDemandForecastingGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<TalentSupplyDemandForecastingReadinessDto>.Success(TalentSupplyDemandForecastingMapper.ToDto(entity));
    }
}
