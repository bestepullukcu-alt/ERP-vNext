using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Handlers;

public sealed class EvaluateWorkforcePlanningReadinessHandler : IRequestHandler<EvaluateWorkforcePlanningReadinessCommand, Response<WorkforcePlanningReadinessDto>>
{
    private readonly IWorkforcePlanningReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateWorkforcePlanningReadinessHandler(IWorkforcePlanningReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<WorkforcePlanningReadinessDto>> Handle(EvaluateWorkforcePlanningReadinessCommand request, CancellationToken ct)
    {
        var tenant = WorkforcePlanningGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<WorkforcePlanningReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<WorkforcePlanningReadinessDto>.Fail("WorkforcePlanning readiness record was not found.", 404);
        }

        WorkforcePlanningGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<WorkforcePlanningReadinessDto>.Success(WorkforcePlanningMapper.ToDto(entity));
    }
}
