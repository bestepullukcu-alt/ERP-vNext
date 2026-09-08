using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Handlers;

public sealed class GetSectorTalentTrendsReadinessListHandler
    : IRequestHandler<GetSectorTalentTrendsReadinessListQuery, Response<IReadOnlyList<SectorTalentTrendsReadinessListItemDto>>>
{
    private readonly ISectorTalentTrendsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSectorTalentTrendsReadinessListHandler(ISectorTalentTrendsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<SectorTalentTrendsReadinessListItemDto>>> Handle(
        GetSectorTalentTrendsReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = SectorTalentTrendsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<SectorTalentTrendsReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<SectorTalentTrendsReadinessListItemDto>>.Success(rows.Select(SectorTalentTrendsMapper.ToListItem).ToList());
    }
}

public sealed class GetSectorTalentTrendsReadinessByIdHandler
    : IRequestHandler<GetSectorTalentTrendsReadinessByIdQuery, Response<SectorTalentTrendsReadinessDto>>
{
    private readonly ISectorTalentTrendsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSectorTalentTrendsReadinessByIdHandler(ISectorTalentTrendsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SectorTalentTrendsReadinessDto>> Handle(GetSectorTalentTrendsReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = SectorTalentTrendsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SectorTalentTrendsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<SectorTalentTrendsReadinessDto>.Fail("SectorTalentTrends readiness record was not found.", 404)
            : Response<SectorTalentTrendsReadinessDto>.Success(SectorTalentTrendsMapper.ToDto(entity));
    }
}

public sealed class GetSectorTalentTrendsAuditMetadataHandler
    : IRequestHandler<GetSectorTalentTrendsAuditMetadataQuery, Response<SectorTalentTrendsAuditMetadataDto>>
{
    private readonly ISectorTalentTrendsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSectorTalentTrendsAuditMetadataHandler(ISectorTalentTrendsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SectorTalentTrendsAuditMetadataDto>> Handle(GetSectorTalentTrendsAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = SectorTalentTrendsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<SectorTalentTrendsAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<SectorTalentTrendsAuditMetadataDto>.Fail("SectorTalentTrends readiness record was not found.", 404)
            : Response<SectorTalentTrendsAuditMetadataDto>.Success(SectorTalentTrendsMapper.ToAuditMetadata(entity));
    }
}
