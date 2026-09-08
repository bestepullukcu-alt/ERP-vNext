using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Queries;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Handlers;

public sealed class GetMetricDefinitionsOwnershipReadinessListHandler
    : IRequestHandler<GetMetricDefinitionsOwnershipReadinessListQuery, Response<IReadOnlyList<MetricDefinitionsOwnershipReadinessListItemDto>>>
{
    private readonly IMetricDefinitionsOwnershipReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetMetricDefinitionsOwnershipReadinessListHandler(IMetricDefinitionsOwnershipReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<MetricDefinitionsOwnershipReadinessListItemDto>>> Handle(
        GetMetricDefinitionsOwnershipReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = MetricDefinitionsOwnershipGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<MetricDefinitionsOwnershipReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<MetricDefinitionsOwnershipReadinessListItemDto>>.Success(rows.Select(MetricDefinitionsOwnershipMapper.ToListItem).ToList());
    }
}

public sealed class GetMetricDefinitionsOwnershipReadinessByIdHandler
    : IRequestHandler<GetMetricDefinitionsOwnershipReadinessByIdQuery, Response<MetricDefinitionsOwnershipReadinessDto>>
{
    private readonly IMetricDefinitionsOwnershipReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetMetricDefinitionsOwnershipReadinessByIdHandler(IMetricDefinitionsOwnershipReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<MetricDefinitionsOwnershipReadinessDto>> Handle(GetMetricDefinitionsOwnershipReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = MetricDefinitionsOwnershipGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<MetricDefinitionsOwnershipReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<MetricDefinitionsOwnershipReadinessDto>.Fail("MetricDefinitionsOwnership readiness record was not found.", 404)
            : Response<MetricDefinitionsOwnershipReadinessDto>.Success(MetricDefinitionsOwnershipMapper.ToDto(entity));
    }
}

public sealed class GetMetricDefinitionsOwnershipAuditMetadataHandler
    : IRequestHandler<GetMetricDefinitionsOwnershipAuditMetadataQuery, Response<MetricDefinitionsOwnershipAuditMetadataDto>>
{
    private readonly IMetricDefinitionsOwnershipReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetMetricDefinitionsOwnershipAuditMetadataHandler(IMetricDefinitionsOwnershipReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<MetricDefinitionsOwnershipAuditMetadataDto>> Handle(GetMetricDefinitionsOwnershipAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = MetricDefinitionsOwnershipGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<MetricDefinitionsOwnershipAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<MetricDefinitionsOwnershipAuditMetadataDto>.Fail("MetricDefinitionsOwnership readiness record was not found.", 404)
            : Response<MetricDefinitionsOwnershipAuditMetadataDto>.Success(MetricDefinitionsOwnershipMapper.ToAuditMetadata(entity));
    }
}
