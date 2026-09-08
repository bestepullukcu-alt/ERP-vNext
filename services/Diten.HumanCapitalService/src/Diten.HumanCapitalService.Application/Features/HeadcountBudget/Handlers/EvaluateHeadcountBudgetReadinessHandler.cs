using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget.Handlers;

public sealed class EvaluateHeadcountBudgetReadinessHandler : IRequestHandler<EvaluateHeadcountBudgetReadinessCommand, Response<HeadcountBudgetReadinessDto>>
{
    private readonly IHeadcountBudgetReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateHeadcountBudgetReadinessHandler(IHeadcountBudgetReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<HeadcountBudgetReadinessDto>> Handle(EvaluateHeadcountBudgetReadinessCommand request, CancellationToken ct)
    {
        var tenant = HeadcountBudgetGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HeadcountBudgetReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<HeadcountBudgetReadinessDto>.Fail("HeadcountBudget readiness record was not found.", 404);
        }

        HeadcountBudgetGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<HeadcountBudgetReadinessDto>.Success(HeadcountBudgetMapper.ToDto(entity));
    }
}
