using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Handlers;

public sealed class EvaluateEarlyWarningSignalsReadinessHandler : IRequestHandler<EvaluateEarlyWarningSignalsReadinessCommand, Response<EarlyWarningSignalsReadinessDto>>
{
    private readonly IEarlyWarningSignalsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateEarlyWarningSignalsReadinessHandler(IEarlyWarningSignalsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<EarlyWarningSignalsReadinessDto>> Handle(EvaluateEarlyWarningSignalsReadinessCommand request, CancellationToken ct)
    {
        var tenant = EarlyWarningSignalsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EarlyWarningSignalsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<EarlyWarningSignalsReadinessDto>.Fail("EarlyWarningSignals readiness record was not found.", 404);
        }

        EarlyWarningSignalsGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<EarlyWarningSignalsReadinessDto>.Success(EarlyWarningSignalsMapper.ToDto(entity));
    }
}
