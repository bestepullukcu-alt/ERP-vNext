using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrCaseManagement.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCaseManagement.Handlers;

public sealed class EvaluateHrCaseManagementReadinessHandler : IRequestHandler<EvaluateHrCaseManagementReadinessCommand, Response<HrCaseManagementReadinessDto>>
{
    private readonly IHrCaseManagementReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateHrCaseManagementReadinessHandler(IHrCaseManagementReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<HrCaseManagementReadinessDto>> Handle(EvaluateHrCaseManagementReadinessCommand request, CancellationToken ct)
    {
        var tenant = HrCaseManagementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrCaseManagementReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<HrCaseManagementReadinessDto>.Fail("HrCaseManagement readiness record was not found.", 404);
        }

        HrCaseManagementGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<HrCaseManagementReadinessDto>.Success(HrCaseManagementMapper.ToDto(entity));
    }
}
