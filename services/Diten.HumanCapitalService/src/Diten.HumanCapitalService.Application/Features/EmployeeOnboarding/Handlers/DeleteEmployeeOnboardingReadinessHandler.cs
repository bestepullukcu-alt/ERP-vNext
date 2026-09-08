using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Handlers;

public sealed class DeleteEmployeeOnboardingReadinessHandler : IRequestHandler<DeleteEmployeeOnboardingReadinessCommand, Response<bool>>
{
    private readonly IEmployeeOnboardingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteEmployeeOnboardingReadinessHandler(IEmployeeOnboardingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteEmployeeOnboardingReadinessCommand request, CancellationToken ct)
    {
        var tenant = EmployeeOnboardingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("EmployeeOnboarding readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.OnboardingReadinessState = EmployeeOnboardingReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
