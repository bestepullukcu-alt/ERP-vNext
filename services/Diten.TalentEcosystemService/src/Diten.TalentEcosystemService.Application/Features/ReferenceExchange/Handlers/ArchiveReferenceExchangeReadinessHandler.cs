using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Commands;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Handlers;

public sealed class ArchiveReferenceExchangeReadinessHandler : IRequestHandler<ArchiveReferenceExchangeReadinessCommand, Response<NoContent>>
{
    private readonly ITepReferenceExchangeMarketplaceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public ArchiveReferenceExchangeReadinessHandler(
        ITepReferenceExchangeMarketplaceReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<NoContent>> Handle(ArchiveReferenceExchangeReadinessCommand request, CancellationToken ct)
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

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.ExchangeReadinessState = TepReferenceExchangeReadinessState.Archived;
        entity.UpdatedAt = entity.DeletedAt;
        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
