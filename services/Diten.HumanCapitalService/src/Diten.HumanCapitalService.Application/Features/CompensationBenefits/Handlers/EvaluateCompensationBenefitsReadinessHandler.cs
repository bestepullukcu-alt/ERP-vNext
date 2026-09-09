using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CompensationBenefits.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompensationBenefits.Handlers;

public sealed class EvaluateCompensationBenefitsReadinessHandler : IRequestHandler<EvaluateCompensationBenefitsReadinessCommand, Response<CompensationBenefitsReadinessDto>>
{
    private readonly ICompensationBenefitsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateCompensationBenefitsReadinessHandler(ICompensationBenefitsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CompensationBenefitsReadinessDto>> Handle(EvaluateCompensationBenefitsReadinessCommand request, CancellationToken ct)
    {
        var tenant = CompensationBenefitsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CompensationBenefitsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<CompensationBenefitsReadinessDto>.Fail("CompensationBenefits readiness record was not found.", 404);
        }

        CompensationBenefitsGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<CompensationBenefitsReadinessDto>.Success(CompensationBenefitsMapper.ToDto(entity));
    }
}
