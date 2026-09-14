using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Handlers;

public sealed class EvaluateTalentDevelopmentNetworkReadinessHandler : IRequestHandler<EvaluateTalentDevelopmentNetworkReadinessCommand, Response<TalentDevelopmentNetworkReadinessDto>>
{
    private readonly ITalentDevelopmentNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateTalentDevelopmentNetworkReadinessHandler(ITalentDevelopmentNetworkReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<TalentDevelopmentNetworkReadinessDto>> Handle(EvaluateTalentDevelopmentNetworkReadinessCommand request, CancellationToken ct)
    {
        var tenant = TalentDevelopmentNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TalentDevelopmentNetworkReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<TalentDevelopmentNetworkReadinessDto>.Fail("TalentDevelopmentNetwork readiness record was not found.", 404);
        }

        TalentDevelopmentNetworkGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<TalentDevelopmentNetworkReadinessDto>.Success(TalentDevelopmentNetworkMapper.ToDto(entity));
    }
}
