using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Handlers;

public sealed class GetReferenceExchangeReadinessListHandler
    : IRequestHandler<GetReferenceExchangeReadinessListQuery, Response<IReadOnlyList<ReferenceExchangeReadinessListItemDto>>>
{
    private readonly ITepReferenceExchangeMarketplaceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetReferenceExchangeReadinessListHandler(
        ITepReferenceExchangeMarketplaceReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<ReferenceExchangeReadinessListItemDto>>> Handle(
        GetReferenceExchangeReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = ReferenceExchangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<ReferenceExchangeReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var items = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<ReferenceExchangeReadinessListItemDto>>.Success(items.Select(ReferenceExchangeMapper.ToListItemDto).ToList());
    }
}

public sealed class GetReferenceExchangeReadinessByIdHandler
    : IRequestHandler<GetReferenceExchangeReadinessByIdQuery, Response<ReferenceExchangeReadinessDto>>
{
    private readonly ITepReferenceExchangeMarketplaceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetReferenceExchangeReadinessByIdHandler(
        ITepReferenceExchangeMarketplaceReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<ReferenceExchangeReadinessDto>> Handle(GetReferenceExchangeReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = ReferenceExchangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ReferenceExchangeReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var item = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return item is null
            ? Response<ReferenceExchangeReadinessDto>.Fail("Reference exchange readiness record was not found.", 404)
            : Response<ReferenceExchangeReadinessDto>.Success(ReferenceExchangeMapper.ToDto(item));
    }
}

public sealed class GetReferenceExchangeAuditMetadataHandler
    : IRequestHandler<GetReferenceExchangeAuditMetadataQuery, Response<ReferenceExchangeAuditMetadataDto>>
{
    private readonly ITepReferenceExchangeMarketplaceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetReferenceExchangeAuditMetadataHandler(
        ITepReferenceExchangeMarketplaceReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<ReferenceExchangeAuditMetadataDto>> Handle(GetReferenceExchangeAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = ReferenceExchangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ReferenceExchangeAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var item = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return item is null
            ? Response<ReferenceExchangeAuditMetadataDto>.Fail("Reference exchange readiness record was not found.", 404)
            : Response<ReferenceExchangeAuditMetadataDto>.Success(new ReferenceExchangeAuditMetadataDto(
                item.Id,
                item.EvidenceRetentionState,
                item.AuditReadinessState,
                item.AbuseControlState,
                item.ThrottlingPolicyState,
                item.ReviewDisputeBoundaryState,
                item.NotificationDependencyState,
                item.DocumentDependencyState,
                item.DependencyStates.Select(ReferenceExchangeMapper.ToDto).ToList(),
                item.DeferredReason));
    }
}
