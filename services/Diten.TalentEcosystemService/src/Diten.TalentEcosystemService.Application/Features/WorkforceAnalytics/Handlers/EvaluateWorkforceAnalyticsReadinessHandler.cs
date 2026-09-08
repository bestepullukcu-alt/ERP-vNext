using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics.Handlers;

public sealed class EvaluateWorkforceAnalyticsReadinessHandler : IRequestHandler<EvaluateWorkforceAnalyticsReadinessCommand, Response<WorkforceAnalyticsReadinessDto>>
{
    private readonly IWorkforceAnalyticsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateWorkforceAnalyticsReadinessHandler(IWorkforceAnalyticsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<WorkforceAnalyticsReadinessDto>> Handle(EvaluateWorkforceAnalyticsReadinessCommand request, CancellationToken ct)
    {
        var tenant = WorkforceAnalyticsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<WorkforceAnalyticsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<WorkforceAnalyticsReadinessDto>.Fail("WorkforceAnalytics readiness record was not found.", 404);
        }

        WorkforceAnalyticsGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<WorkforceAnalyticsReadinessDto>.Success(WorkforceAnalyticsMapper.ToDto(entity));
    }
}
