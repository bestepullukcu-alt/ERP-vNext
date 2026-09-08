using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Handlers;

public sealed class DeleteSectorTalentTrendsReadinessHandler : IRequestHandler<DeleteSectorTalentTrendsReadinessCommand, Response<bool>>
{
    private readonly ISectorTalentTrendsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteSectorTalentTrendsReadinessHandler(ISectorTalentTrendsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteSectorTalentTrendsReadinessCommand request, CancellationToken ct)
    {
        var tenant = SectorTalentTrendsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("SectorTalentTrends readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.SectorTalentTrendsReadinessState = SectorTalentTrendsReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
