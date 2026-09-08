using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.SelfService.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SelfService.Handlers;

public sealed class EvaluateSelfServiceReadinessHandler : IRequestHandler<EvaluateSelfServiceReadinessCommand, Response<SelfServiceReadinessDto>>
{
    private readonly ISelfServiceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateSelfServiceReadinessHandler(ISelfServiceReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SelfServiceReadinessDto>> Handle(EvaluateSelfServiceReadinessCommand request, CancellationToken ct)
    {
        var tenant = SelfServiceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SelfServiceReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<SelfServiceReadinessDto>.Fail("SelfService readiness record was not found.", 404);
        }

        SelfServiceGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<SelfServiceReadinessDto>.Success(SelfServiceMapper.ToDto(entity));
    }
}
