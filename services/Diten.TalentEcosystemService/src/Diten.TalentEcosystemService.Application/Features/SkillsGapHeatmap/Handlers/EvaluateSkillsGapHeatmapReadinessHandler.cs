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

    public EvaluateSkillsGapHeatmapReadinessHandler(ISkillsGapHeatmapReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SkillsGapHeatmapReadinessDto>> Handle(EvaluateSkillsGapHeatmapReadinessCommand request, CancellationToken ct)
    {
        var tenant = SkillsGapHeatmapGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SkillsGapHeatmapReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<SkillsGapHeatmapReadinessDto>.Fail("SkillsGapHeatmap readiness record was not found.", 404);
        }

        SkillsGapHeatmapGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<SkillsGapHeatmapReadinessDto>.Success(SkillsGapHeatmapMapper.ToDto(entity));
    }
}
