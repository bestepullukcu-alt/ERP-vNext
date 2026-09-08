using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Queries;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Handlers;

public sealed class GetMetricSemanticRegistryReadinessListHandler
    : IRequestHandler<GetMetricSemanticRegistryReadinessListQuery, Response<IReadOnlyList<MetricSemanticRegistryReadinessListItemDto>>>
{
    private readonly IMetricSemanticRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetMetricSemanticRegistryReadinessListHandler(IMetricSemanticRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<MetricSemanticRegistryReadinessListItemDto>>> Handle(
        GetMetricSemanticRegistryReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = MetricSemanticRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<MetricSemanticRegistryReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<MetricSemanticRegistryReadinessListItemDto>>.Success(rows.Select(MetricSemanticRegistryMapper.ToListItem).ToList());
    }
}

public sealed class GetMetricSemanticRegistryReadinessByIdHandler
    : IRequestHandler<GetMetricSemanticRegistryReadinessByIdQuery, Response<MetricSemanticRegistryReadinessDto>>
{
    private readonly IMetricSemanticRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetMetricSemanticRegistryReadinessByIdHandler(IMetricSemanticRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<MetricSemanticRegistryReadinessDto>> Handle(GetMetricSemanticRegistryReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = MetricSemanticRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<MetricSemanticRegistryReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<MetricSemanticRegistryReadinessDto>.Fail("MetricSemanticRegistry readiness record was not found.", 404)
            : Response<MetricSemanticRegistryReadinessDto>.Success(MetricSemanticRegistryMapper.ToDto(entity));
    }
}

public sealed class GetMetricSemanticRegistryAuditMetadataHandler
    : IRequestHandler<GetMetricSemanticRegistryAuditMetadataQuery, Response<MetricSemanticRegistryAuditMetadataDto>>
{
    private readonly IMetricSemanticRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetMetricSemanticRegistryAuditMetadataHandler(IMetricSemanticRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<MetricSemanticRegistryAuditMetadataDto>> Handle(GetMetricSemanticRegistryAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = MetricSemanticRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<MetricSemanticRegistryAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<MetricSemanticRegistryAuditMetadataDto>.Fail("MetricSemanticRegistry readiness record was not found.", 404)
            : Response<MetricSemanticRegistryAuditMetadataDto>.Success(MetricSemanticRegistryMapper.ToAuditMetadata(entity));
    }
}
