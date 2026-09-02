using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Handlers;

public sealed class CreateReferenceExchangeReadinessHandler : IRequestHandler<CreateReferenceExchangeReadinessCommand, Response<Guid>>
{
    private readonly ITepReferenceExchangeMarketplaceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateReferenceExchangeReadinessHandler(
        ITepReferenceExchangeMarketplaceReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateReferenceExchangeReadinessCommand request, CancellationToken ct)
    {
        var tenant = ReferenceExchangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var validation = ReferenceExchangeGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<Guid>.Fail(validation, 400);
        }

        var readiness = ReferenceExchangeGuard.ValidateReadinessRequest(request.Request);
        if (!readiness.IsSuccessful)
        {
            return Response<Guid>.Fail(readiness.Errors, readiness.StatusCode);
        }

        var entity = ReferenceExchangeHandlerMapper.ToEntity(tenant.Data, request.Request);
        if (await _repository.ExistsActiveCodeAsync(tenant.Data, request.Request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("An active reference exchange readiness record with the same Code already exists for this tenant.", 409);
        }

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
