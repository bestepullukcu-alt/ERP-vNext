using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Queries;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Handlers;

public sealed class GetDataWarehouseLakehouseReadinessListHandler
    : IRequestHandler<GetDataWarehouseLakehouseReadinessListQuery, Response<IReadOnlyList<DataWarehouseLakehouseReadinessListItemDto>>>
{
    private readonly IDataWarehouseLakehouseReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetDataWarehouseLakehouseReadinessListHandler(IDataWarehouseLakehouseReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<DataWarehouseLakehouseReadinessListItemDto>>> Handle(
        GetDataWarehouseLakehouseReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = DataWarehouseLakehouseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<DataWarehouseLakehouseReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<DataWarehouseLakehouseReadinessListItemDto>>.Success(rows.Select(DataWarehouseLakehouseMapper.ToListItem).ToList());
    }
}

public sealed class GetDataWarehouseLakehouseReadinessByIdHandler
    : IRequestHandler<GetDataWarehouseLakehouseReadinessByIdQuery, Response<DataWarehouseLakehouseReadinessDto>>
{
    private readonly IDataWarehouseLakehouseReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetDataWarehouseLakehouseReadinessByIdHandler(IDataWarehouseLakehouseReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<DataWarehouseLakehouseReadinessDto>> Handle(GetDataWarehouseLakehouseReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = DataWarehouseLakehouseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<DataWarehouseLakehouseReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<DataWarehouseLakehouseReadinessDto>.Fail("DataWarehouseLakehouse readiness record was not found.", 404)
            : Response<DataWarehouseLakehouseReadinessDto>.Success(DataWarehouseLakehouseMapper.ToDto(entity));
    }
}

public sealed class GetDataWarehouseLakehouseAuditMetadataHandler
    : IRequestHandler<GetDataWarehouseLakehouseAuditMetadataQuery, Response<DataWarehouseLakehouseAuditMetadataDto>>
{
    private readonly IDataWarehouseLakehouseReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetDataWarehouseLakehouseAuditMetadataHandler(IDataWarehouseLakehouseReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<DataWarehouseLakehouseAuditMetadataDto>> Handle(GetDataWarehouseLakehouseAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = DataWarehouseLakehouseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<DataWarehouseLakehouseAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<DataWarehouseLakehouseAuditMetadataDto>.Fail("DataWarehouseLakehouse readiness record was not found.", 404)
            : Response<DataWarehouseLakehouseAuditMetadataDto>.Success(DataWarehouseLakehouseMapper.ToAuditMetadata(entity));
    }
}
