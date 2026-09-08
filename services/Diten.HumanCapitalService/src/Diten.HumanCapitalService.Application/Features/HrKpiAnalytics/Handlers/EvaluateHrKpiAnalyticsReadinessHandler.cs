using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Handlers;

public sealed class EvaluateHrKpiAnalyticsReadinessHandler : IRequestHandler<EvaluateHrKpiAnalyticsReadinessCommand, Response<HrKpiAnalyticsReadinessDto>>
{
    private readonly IHrKpiAnalyticsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateHrKpiAnalyticsReadinessHandler(IHrKpiAnalyticsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<HrKpiAnalyticsReadinessDto>> Handle(EvaluateHrKpiAnalyticsReadinessCommand request, CancellationToken ct)
    {
        var tenant = HrKpiAnalyticsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrKpiAnalyticsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<HrKpiAnalyticsReadinessDto>.Fail("HrKpiAnalytics readiness record was not found.", 404);
        }

        HrKpiAnalyticsGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<HrKpiAnalyticsReadinessDto>.Success(HrKpiAnalyticsMapper.ToDto(entity));
    }
}
