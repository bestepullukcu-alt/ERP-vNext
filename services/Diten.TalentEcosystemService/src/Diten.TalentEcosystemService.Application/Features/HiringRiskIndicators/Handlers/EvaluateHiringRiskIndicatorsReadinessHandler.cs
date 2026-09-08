using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Handlers;

public sealed class EvaluateHiringRiskIndicatorsReadinessHandler : IRequestHandler<EvaluateHiringRiskIndicatorsReadinessCommand, Response<HiringRiskIndicatorsReadinessDto>>
{
    private readonly IHiringRiskIndicatorsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateHiringRiskIndicatorsReadinessHandler(IHiringRiskIndicatorsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<HiringRiskIndicatorsReadinessDto>> Handle(EvaluateHiringRiskIndicatorsReadinessCommand request, CancellationToken ct)
    {
        var tenant = HiringRiskIndicatorsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HiringRiskIndicatorsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<HiringRiskIndicatorsReadinessDto>.Fail("HiringRiskIndicators readiness record was not found.", 404);
        }

        HiringRiskIndicatorsGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<HiringRiskIndicatorsReadinessDto>.Success(HiringRiskIndicatorsMapper.ToDto(entity));
    }
}
