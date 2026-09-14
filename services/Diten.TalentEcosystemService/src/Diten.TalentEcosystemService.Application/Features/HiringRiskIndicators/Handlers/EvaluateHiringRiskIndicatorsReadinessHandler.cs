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
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateHiringRiskIndicatorsReadinessHandler(IHiringRiskIndicatorsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<HiringRiskIndicatorsReadinessDto>> Handle(EvaluateHiringRiskIndicatorsReadinessCommand request, CancellationToken ct)
    {
        var tenant = HiringRiskIndicatorsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HiringRiskIndicatorsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<HiringRiskIndicatorsReadinessDto>.Fail("HiringRiskIndicators readiness record was not found.", 404);
        }

        HiringRiskIndicatorsGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<HiringRiskIndicatorsReadinessDto>.Success(HiringRiskIndicatorsMapper.ToDto(entity));
    }
}
