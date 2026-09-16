using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Handlers;

public sealed class EvaluateSkillsGapHeatmapReadinessHandler : IRequestHandler<EvaluateSkillsGapHeatmapReadinessCommand, Response<SkillsGapHeatmapReadinessDto>>
{
    private readonly ISkillsGapHeatmapReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateSkillsGapHeatmapReadinessHandler(ISkillsGapHeatmapReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<SkillsGapHeatmapReadinessDto>> Handle(EvaluateSkillsGapHeatmapReadinessCommand request, CancellationToken ct)
    {
        var tenant = SkillsGapHeatmapGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SkillsGapHeatmapReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<SkillsGapHeatmapReadinessDto>.Fail("SkillsGapHeatmap readiness record was not found.", 404);
        }

        SkillsGapHeatmapGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<SkillsGapHeatmapReadinessDto>.Success(SkillsGapHeatmapMapper.ToDto(entity));
    }
}
