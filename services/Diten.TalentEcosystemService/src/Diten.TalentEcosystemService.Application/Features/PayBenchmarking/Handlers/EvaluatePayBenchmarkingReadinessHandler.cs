using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Handlers;

public sealed class EvaluatePayBenchmarkingReadinessHandler : IRequestHandler<EvaluatePayBenchmarkingReadinessCommand, Response<PayBenchmarkingReadinessDto>>
{
    private readonly IPayBenchmarkingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluatePayBenchmarkingReadinessHandler(IPayBenchmarkingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<PayBenchmarkingReadinessDto>> Handle(EvaluatePayBenchmarkingReadinessCommand request, CancellationToken ct)
    {
        var tenant = PayBenchmarkingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<PayBenchmarkingReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<PayBenchmarkingReadinessDto>.Fail("PayBenchmarking readiness record was not found.", 404);
        }

        PayBenchmarkingGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<PayBenchmarkingReadinessDto>.Success(PayBenchmarkingMapper.ToDto(entity));
    }
}
