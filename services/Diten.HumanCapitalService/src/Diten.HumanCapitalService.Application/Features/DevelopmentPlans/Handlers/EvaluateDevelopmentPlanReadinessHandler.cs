using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Handlers;

public sealed class EvaluateDevelopmentPlanReadinessHandler : IRequestHandler<EvaluateDevelopmentPlanReadinessCommand, Response<DevelopmentPlanReadinessDto>>
{
    private readonly IDevelopmentPlanReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateDevelopmentPlanReadinessHandler(IDevelopmentPlanReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<DevelopmentPlanReadinessDto>> Handle(EvaluateDevelopmentPlanReadinessCommand request, CancellationToken ct)
    {
        var tenant = DevelopmentPlanGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<DevelopmentPlanReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<DevelopmentPlanReadinessDto>.Fail("DevelopmentPlan readiness record was not found.", 404);
        }

        DevelopmentPlanGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<DevelopmentPlanReadinessDto>.Success(DevelopmentPlanMapper.ToDto(entity));
    }
}
