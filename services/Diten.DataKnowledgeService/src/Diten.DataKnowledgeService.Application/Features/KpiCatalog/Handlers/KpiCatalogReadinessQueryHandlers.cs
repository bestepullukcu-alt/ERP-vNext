using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog.Queries;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.KpiCatalog.Handlers;

public sealed class GetKpiCatalogReadinessListHandler
    : IRequestHandler<GetKpiCatalogReadinessListQuery, Response<IReadOnlyList<KpiCatalogReadinessListItemDto>>>
{
    private readonly IKpiCatalogReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetKpiCatalogReadinessListHandler(IKpiCatalogReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<KpiCatalogReadinessListItemDto>>> Handle(
        GetKpiCatalogReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = KpiCatalogGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<KpiCatalogReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<KpiCatalogReadinessListItemDto>>.Success(rows.Select(KpiCatalogMapper.ToListItem).ToList());
    }
}

public sealed class GetKpiCatalogReadinessByIdHandler
    : IRequestHandler<GetKpiCatalogReadinessByIdQuery, Response<KpiCatalogReadinessDto>>
{
    private readonly IKpiCatalogReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetKpiCatalogReadinessByIdHandler(IKpiCatalogReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<KpiCatalogReadinessDto>> Handle(GetKpiCatalogReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = KpiCatalogGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<KpiCatalogReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<KpiCatalogReadinessDto>.Fail("KpiCatalog readiness record was not found.", 404)
            : Response<KpiCatalogReadinessDto>.Success(KpiCatalogMapper.ToDto(entity));
    }
}

public sealed class GetKpiCatalogAuditMetadataHandler
    : IRequestHandler<GetKpiCatalogAuditMetadataQuery, Response<KpiCatalogAuditMetadataDto>>
{
    private readonly IKpiCatalogReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetKpiCatalogAuditMetadataHandler(IKpiCatalogReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<KpiCatalogAuditMetadataDto>> Handle(GetKpiCatalogAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = KpiCatalogGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<KpiCatalogAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<KpiCatalogAuditMetadataDto>.Fail("KpiCatalog readiness record was not found.", 404)
            : Response<KpiCatalogAuditMetadataDto>.Success(KpiCatalogMapper.ToAuditMetadata(entity));
    }
}
