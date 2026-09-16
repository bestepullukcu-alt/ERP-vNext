using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Handlers;

public sealed class UpdateReferenceExchangeReadinessHandler : IRequestHandler<UpdateReferenceExchangeReadinessCommand, Response<NoContent>>
{
    private readonly ITepReferenceExchangeMarketplaceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public UpdateReferenceExchangeReadinessHandler(
        ITepReferenceExchangeMarketplaceReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<NoContent>> Handle(UpdateReferenceExchangeReadinessCommand request, CancellationToken ct)
    {
        var tenant = ReferenceExchangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<NoContent>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Reference exchange readiness record was not found.", 404);
        }

        var validation = ReferenceExchangeGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<NoContent>.Fail(validation, 400);
        }

        var readiness = ReferenceExchangeGuard.ValidateReadinessRequest(request.Request);
        if (!readiness.IsSuccessful)
        {
            return Response<NoContent>.Fail(readiness.Errors, readiness.StatusCode);
        }

        if (await _repository.ExistsActiveCodeAsync(tenant.Data, entity.LegalEntityId, request.Request.Code.Trim(), request.Id, ct))
        {
            return Response<NoContent>.Fail("An active reference exchange readiness record with the same Code already exists for this tenant.", 409);
        }

        ReferenceExchangeHandlerMapper.Apply(entity, request.Request);
        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
