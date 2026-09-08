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

    public GetRestrictedIntegrityRegistryReadinessListHandler(IRestrictedIntegrityRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
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

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<RestrictedIntegrityRegistryReadinessListItemDto>>.Success(rows.Select(RestrictedIntegrityRegistryMapper.ToListItem).ToList());
    }
}

public sealed class GetRestrictedIntegrityRegistryReadinessByIdHandler
    : IRequestHandler<GetRestrictedIntegrityRegistryReadinessByIdQuery, Response<RestrictedIntegrityRegistryReadinessDto>>
{
    private readonly IRestrictedIntegrityRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetRestrictedIntegrityRegistryReadinessByIdHandler(IRestrictedIntegrityRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<RestrictedIntegrityRegistryReadinessDto>> Handle(GetRestrictedIntegrityRegistryReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = RestrictedIntegrityRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<RestrictedIntegrityRegistryReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
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

    public GetRestrictedIntegrityRegistryAuditMetadataHandler(IRestrictedIntegrityRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<RestrictedIntegrityRegistryAuditMetadataDto>> Handle(GetRestrictedIntegrityRegistryAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = RestrictedIntegrityRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<RestrictedIntegrityRegistryAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<RestrictedIntegrityRegistryAuditMetadataDto>.Fail("RestrictedIntegrityRegistry readiness record was not found.", 404)
            : Response<RestrictedIntegrityRegistryAuditMetadataDto>.Success(RestrictedIntegrityRegistryMapper.ToAuditMetadata(entity));
    }
}
