using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Handlers;

public sealed class EvaluateTalentDataFoundationReadinessHandler : IRequestHandler<EvaluateTalentDataFoundationReadinessCommand, Response<TalentDataFoundationReadinessDto>>
{
    private readonly ITalentDataFoundationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateTalentDataFoundationReadinessHandler(ITalentDataFoundationReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TalentDataFoundationReadinessDto>> Handle(EvaluateTalentDataFoundationReadinessCommand request, CancellationToken ct)
    {
        var tenant = TalentDataFoundationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TalentDataFoundationReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<TalentDataFoundationReadinessDto>.Fail("TalentDataFoundation readiness record was not found.", 404);
        }

        TalentDataFoundationGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<TalentDataFoundationReadinessDto>.Success(TalentDataFoundationMapper.ToDto(entity));
    }
}
