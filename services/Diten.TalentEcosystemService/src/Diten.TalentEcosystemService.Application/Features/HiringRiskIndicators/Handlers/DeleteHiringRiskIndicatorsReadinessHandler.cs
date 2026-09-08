using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Handlers;

public sealed class DeleteHiringRiskIndicatorsReadinessHandler : IRequestHandler<DeleteHiringRiskIndicatorsReadinessCommand, Response<bool>>
{
    private readonly IHiringRiskIndicatorsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteHiringRiskIndicatorsReadinessHandler(IHiringRiskIndicatorsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteHiringRiskIndicatorsReadinessCommand request, CancellationToken ct)
    {
        var tenant = HiringRiskIndicatorsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("HiringRiskIndicators readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.HiringRiskIndicatorsReadinessState = HiringRiskIndicatorsReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
