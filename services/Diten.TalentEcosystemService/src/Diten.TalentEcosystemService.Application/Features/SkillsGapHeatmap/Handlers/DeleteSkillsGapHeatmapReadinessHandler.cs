using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Handlers;

public sealed class DeleteSkillsGapHeatmapReadinessHandler : IRequestHandler<DeleteSkillsGapHeatmapReadinessCommand, Response<bool>>
{
    private readonly ISkillsGapHeatmapReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteSkillsGapHeatmapReadinessHandler(ISkillsGapHeatmapReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteSkillsGapHeatmapReadinessCommand request, CancellationToken ct)
    {
        var tenant = SkillsGapHeatmapGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("SkillsGapHeatmap readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.SkillsGapHeatmapReadinessState = SkillsGapHeatmapReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
