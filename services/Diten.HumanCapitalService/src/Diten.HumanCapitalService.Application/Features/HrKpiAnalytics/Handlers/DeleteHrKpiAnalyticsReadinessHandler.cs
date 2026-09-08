using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Handlers;

public sealed class DeleteHrKpiAnalyticsReadinessHandler : IRequestHandler<DeleteHrKpiAnalyticsReadinessCommand, Response<bool>>
{
    private readonly IHrKpiAnalyticsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteHrKpiAnalyticsReadinessHandler(IHrKpiAnalyticsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteHrKpiAnalyticsReadinessCommand request, CancellationToken ct)
    {
        var tenant = HrKpiAnalyticsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("HrKpiAnalytics readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.HrKpiAnalyticsReadinessState = HrKpiAnalyticsReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
