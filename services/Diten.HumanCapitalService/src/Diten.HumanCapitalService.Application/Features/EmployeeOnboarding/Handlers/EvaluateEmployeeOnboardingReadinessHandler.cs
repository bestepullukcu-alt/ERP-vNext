using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Handlers;

public sealed class EvaluateEmployeeOnboardingReadinessHandler : IRequestHandler<EvaluateEmployeeOnboardingReadinessCommand, Response<EmployeeOnboardingReadinessDto>>
{
    private readonly IEmployeeOnboardingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateEmployeeOnboardingReadinessHandler(IEmployeeOnboardingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<EmployeeOnboardingReadinessDto>> Handle(EvaluateEmployeeOnboardingReadinessCommand request, CancellationToken ct)
    {
        var tenant = EmployeeOnboardingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EmployeeOnboardingReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<EmployeeOnboardingReadinessDto>.Fail("EmployeeOnboarding readiness record was not found.", 404);
        }

        EmployeeOnboardingGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<EmployeeOnboardingReadinessDto>.Success(EmployeeOnboardingMapper.ToDto(entity));
    }
}
