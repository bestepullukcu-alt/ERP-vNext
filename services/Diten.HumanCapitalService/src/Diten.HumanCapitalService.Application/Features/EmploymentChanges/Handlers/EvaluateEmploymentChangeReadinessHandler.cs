using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmploymentChanges.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges.Handlers;

public sealed class EvaluateEmploymentChangeReadinessHandler : IRequestHandler<EvaluateEmploymentChangeReadinessCommand, Response<EmploymentChangeReadinessDto>>
{
    private readonly IEmploymentChangeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateEmploymentChangeReadinessHandler(IEmploymentChangeReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<EmploymentChangeReadinessDto>> Handle(EvaluateEmploymentChangeReadinessCommand request, CancellationToken ct)
    {
        var tenant = EmploymentChangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EmploymentChangeReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<EmploymentChangeReadinessDto>.Fail("EmploymentChange readiness record was not found.", 404);
        }

        EmploymentChangeGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<EmploymentChangeReadinessDto>.Success(EmploymentChangeMapper.ToDto(entity));
    }
}
