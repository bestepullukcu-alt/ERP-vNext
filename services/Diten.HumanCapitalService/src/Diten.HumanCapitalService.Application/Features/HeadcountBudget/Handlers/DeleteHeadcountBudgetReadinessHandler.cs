using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget.Handlers;

public sealed class DeleteHeadcountBudgetReadinessHandler : IRequestHandler<DeleteHeadcountBudgetReadinessCommand, Response<bool>>
{
    private readonly IHeadcountBudgetReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteHeadcountBudgetReadinessHandler(IHeadcountBudgetReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteHeadcountBudgetReadinessCommand request, CancellationToken ct)
    {
        var tenant = HeadcountBudgetGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("HeadcountBudget readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.HeadcountBudgetReadinessState = HeadcountBudgetReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
