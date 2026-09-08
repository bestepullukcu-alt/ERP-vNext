using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Handlers;

public sealed class EvaluateSectorTalentTrendsReadinessHandler : IRequestHandler<EvaluateSectorTalentTrendsReadinessCommand, Response<SectorTalentTrendsReadinessDto>>
{
    private readonly ISectorTalentTrendsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateSectorTalentTrendsReadinessHandler(ISectorTalentTrendsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SectorTalentTrendsReadinessDto>> Handle(EvaluateSectorTalentTrendsReadinessCommand request, CancellationToken ct)
    {
        var tenant = SectorTalentTrendsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SectorTalentTrendsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<SectorTalentTrendsReadinessDto>.Fail("SectorTalentTrends readiness record was not found.", 404);
        }

        SectorTalentTrendsGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<SectorTalentTrendsReadinessDto>.Success(SectorTalentTrendsMapper.ToDto(entity));
    }
}
