using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.OfferManagement.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OfferManagement.Handlers;

public sealed class GetOfferReadinessListHandler
    : IRequestHandler<GetOfferReadinessListQuery, Response<IReadOnlyList<OfferReadinessListItemDto>>>
{
    private readonly IOfferReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetOfferReadinessListHandler(IOfferReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<OfferReadinessListItemDto>>> Handle(
        GetOfferReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = OfferManagementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<OfferReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<OfferReadinessListItemDto>>.Success(rows.Select(OfferManagementMapper.ToListItem).ToList());
    }
}

public sealed class GetOfferReadinessByIdHandler
    : IRequestHandler<GetOfferReadinessByIdQuery, Response<OfferReadinessDto>>
{
    private readonly IOfferReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetOfferReadinessByIdHandler(IOfferReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<OfferReadinessDto>> Handle(GetOfferReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = OfferManagementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<OfferReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<OfferReadinessDto>.Fail("Offer readiness record was not found.", 404)
            : Response<OfferReadinessDto>.Success(OfferManagementMapper.ToDto(entity));
    }
}

public sealed class GetOfferAuditMetadataHandler
    : IRequestHandler<GetOfferAuditMetadataQuery, Response<OfferAuditMetadataDto>>
{
    private readonly IOfferReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetOfferAuditMetadataHandler(IOfferReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<OfferAuditMetadataDto>> Handle(GetOfferAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = OfferManagementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<OfferAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<OfferAuditMetadataDto>.Fail("Offer readiness record was not found.", 404)
            : Response<OfferAuditMetadataDto>.Success(OfferManagementMapper.ToAuditMetadata(entity));
    }
}
