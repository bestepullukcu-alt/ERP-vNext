using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Handlers;

public sealed class GetRestrictedIntegrityRegistryReadinessListHandler
    : IRequestHandler<GetRestrictedIntegrityRegistryReadinessListQuery, Response<IReadOnlyList<RestrictedIntegrityRegistryReadinessListItemDto>>>
{
    private readonly IRestrictedIntegrityRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetRestrictedIntegrityRegistryReadinessListHandler(IRestrictedIntegrityRegistryReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<RestrictedIntegrityRegistryReadinessListItemDto>>> Handle(
        GetRestrictedIntegrityRegistryReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = RestrictedIntegrityRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<RestrictedIntegrityRegistryReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<RestrictedIntegrityRegistryReadinessListItemDto>>.Success(rows.Select(RestrictedIntegrityRegistryMapper.ToListItem).ToList());
    }
}

public sealed class GetRestrictedIntegrityRegistryReadinessByIdHandler
    : IRequestHandler<GetRestrictedIntegrityRegistryReadinessByIdQuery, Response<RestrictedIntegrityRegistryReadinessDto>>
{
    private readonly IRestrictedIntegrityRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetRestrictedIntegrityRegistryReadinessByIdHandler(IRestrictedIntegrityRegistryReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<RestrictedIntegrityRegistryReadinessDto>> Handle(GetRestrictedIntegrityRegistryReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = RestrictedIntegrityRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<RestrictedIntegrityRegistryReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<RestrictedIntegrityRegistryReadinessDto>.Fail("RestrictedIntegrityRegistry readiness record was not found.", 404)
            : Response<RestrictedIntegrityRegistryReadinessDto>.Success(RestrictedIntegrityRegistryMapper.ToDto(entity));
    }
}

public sealed class GetRestrictedIntegrityRegistryAuditMetadataHandler
    : IRequestHandler<GetRestrictedIntegrityRegistryAuditMetadataQuery, Response<RestrictedIntegrityRegistryAuditMetadataDto>>
{
    private readonly IRestrictedIntegrityRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetRestrictedIntegrityRegistryAuditMetadataHandler(IRestrictedIntegrityRegistryReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<RestrictedIntegrityRegistryAuditMetadataDto>> Handle(GetRestrictedIntegrityRegistryAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = RestrictedIntegrityRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<RestrictedIntegrityRegistryAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<RestrictedIntegrityRegistryAuditMetadataDto>.Fail("RestrictedIntegrityRegistry readiness record was not found.", 404)
            : Response<RestrictedIntegrityRegistryAuditMetadataDto>.Success(RestrictedIntegrityRegistryMapper.ToAuditMetadata(entity));
    }
}
