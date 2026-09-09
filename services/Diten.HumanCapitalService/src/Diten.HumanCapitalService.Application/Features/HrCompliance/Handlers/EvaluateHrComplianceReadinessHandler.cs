using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrCompliance.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCompliance.Handlers;

public sealed class EvaluateHrComplianceReadinessHandler : IRequestHandler<EvaluateHrComplianceReadinessCommand, Response<HrComplianceReadinessDto>>
{
    private readonly IHrComplianceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateHrComplianceReadinessHandler(IHrComplianceReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<HrComplianceReadinessDto>> Handle(EvaluateHrComplianceReadinessCommand request, CancellationToken ct)
    {
        var tenant = HrComplianceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrComplianceReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<HrComplianceReadinessDto>.Fail("HrCompliance readiness record was not found.", 404);
        }

        HrComplianceGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<HrComplianceReadinessDto>.Success(HrComplianceMapper.ToDto(entity));
    }
}
