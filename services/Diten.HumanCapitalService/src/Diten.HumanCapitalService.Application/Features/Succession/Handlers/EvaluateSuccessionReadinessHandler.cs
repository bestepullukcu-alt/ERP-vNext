using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.Succession.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.Succession.Handlers;

public sealed class EvaluateSuccessionReadinessHandler : IRequestHandler<EvaluateSuccessionReadinessCommand, Response<SuccessionReadinessDto>>
{
    private readonly ISuccessionReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateSuccessionReadinessHandler(ISuccessionReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<SuccessionReadinessDto>> Handle(EvaluateSuccessionReadinessCommand request, CancellationToken ct)
    {
        var tenant = SuccessionGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SuccessionReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<SuccessionReadinessDto>.Fail("Succession readiness record was not found.", 404);
        }

        SuccessionGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<SuccessionReadinessDto>.Success(SuccessionMapper.ToDto(entity));
    }
}
