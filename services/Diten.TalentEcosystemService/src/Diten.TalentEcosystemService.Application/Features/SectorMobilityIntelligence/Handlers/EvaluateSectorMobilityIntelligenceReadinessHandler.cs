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
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateSectorMobilityIntelligenceReadinessHandler(ISectorMobilityIntelligenceReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<SectorMobilityIntelligenceReadinessDto>> Handle(EvaluateSectorMobilityIntelligenceReadinessCommand request, CancellationToken ct)
    {
        var tenant = SectorMobilityIntelligenceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SectorMobilityIntelligenceReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<SectorMobilityIntelligenceReadinessDto>.Fail("SectorMobilityIntelligence readiness record was not found.", 404);
        }

        SectorMobilityIntelligenceGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<SectorMobilityIntelligenceReadinessDto>.Success(SectorMobilityIntelligenceMapper.ToDto(entity));
    }
}
