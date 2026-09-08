using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Handlers;

public sealed class EvaluateSectorMobilityIntelligenceReadinessHandler : IRequestHandler<EvaluateSectorMobilityIntelligenceReadinessCommand, Response<SectorMobilityIntelligenceReadinessDto>>
{
    private readonly ISectorMobilityIntelligenceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateSectorMobilityIntelligenceReadinessHandler(ISectorMobilityIntelligenceReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SectorMobilityIntelligenceReadinessDto>> Handle(EvaluateSectorMobilityIntelligenceReadinessCommand request, CancellationToken ct)
    {
        var tenant = SectorMobilityIntelligenceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SectorMobilityIntelligenceReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<SectorMobilityIntelligenceReadinessDto>.Fail("SectorMobilityIntelligence readiness record was not found.", 404);
        }

        SectorMobilityIntelligenceGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<SectorMobilityIntelligenceReadinessDto>.Success(SectorMobilityIntelligenceMapper.ToDto(entity));
    }
}
