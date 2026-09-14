using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Handlers;

public sealed class UpdateRehireRecommendationReadinessHandler : IRequestHandler<UpdateRehireRecommendationReadinessCommand, Response<NoContent>>
{
    private readonly ITepRehireRecommendationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public UpdateRehireRecommendationReadinessHandler(
        ITepRehireRecommendationReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<NoContent>> Handle(UpdateRehireRecommendationReadinessCommand request, CancellationToken ct)
    {
        var tenant = RehireRecommendationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<NoContent>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Rehire recommendation readiness record was not found.", 404);
        }

        var validation = RehireRecommendationGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<NoContent>.Fail(validation, 400);
        }

        var readiness = RehireRecommendationGuard.ValidateReadinessRequest(request.Request);
        if (!readiness.IsSuccessful)
        {
            return Response<NoContent>.Fail(readiness.Errors, readiness.StatusCode);
        }

        if (await _repository.ExistsActiveCodeAsync(tenant.Data, entity.LegalEntityId, request.Request.Code.Trim(), request.Id, ct))
        {
            return Response<NoContent>.Fail("An active rehire recommendation readiness record with the same Code already exists for this tenant.", 409);
        }

        RehireRecommendationHandlerMapper.Apply(entity, request.Request);
        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
