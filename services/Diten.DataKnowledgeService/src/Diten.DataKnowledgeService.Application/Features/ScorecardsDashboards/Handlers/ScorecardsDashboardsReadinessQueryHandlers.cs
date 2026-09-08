using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Queries;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Handlers;

public sealed class GetScorecardsDashboardsReadinessListHandler
    : IRequestHandler<GetScorecardsDashboardsReadinessListQuery, Response<IReadOnlyList<ScorecardsDashboardsReadinessListItemDto>>>
{
    private readonly IScorecardsDashboardsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetScorecardsDashboardsReadinessListHandler(IScorecardsDashboardsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<ScorecardsDashboardsReadinessListItemDto>>> Handle(
        GetScorecardsDashboardsReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = ScorecardsDashboardsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<ScorecardsDashboardsReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<ScorecardsDashboardsReadinessListItemDto>>.Success(rows.Select(ScorecardsDashboardsMapper.ToListItem).ToList());
    }
}

public sealed class GetScorecardsDashboardsReadinessByIdHandler
    : IRequestHandler<GetScorecardsDashboardsReadinessByIdQuery, Response<ScorecardsDashboardsReadinessDto>>
{
    private readonly IScorecardsDashboardsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetScorecardsDashboardsReadinessByIdHandler(IScorecardsDashboardsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ScorecardsDashboardsReadinessDto>> Handle(GetScorecardsDashboardsReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = ScorecardsDashboardsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ScorecardsDashboardsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<ScorecardsDashboardsReadinessDto>.Fail("ScorecardsDashboards readiness record was not found.", 404)
            : Response<ScorecardsDashboardsReadinessDto>.Success(ScorecardsDashboardsMapper.ToDto(entity));
    }
}

public sealed class GetScorecardsDashboardsAuditMetadataHandler
    : IRequestHandler<GetScorecardsDashboardsAuditMetadataQuery, Response<ScorecardsDashboardsAuditMetadataDto>>
{
    private readonly IScorecardsDashboardsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetScorecardsDashboardsAuditMetadataHandler(IScorecardsDashboardsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ScorecardsDashboardsAuditMetadataDto>> Handle(GetScorecardsDashboardsAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = ScorecardsDashboardsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ScorecardsDashboardsAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<ScorecardsDashboardsAuditMetadataDto>.Fail("ScorecardsDashboards readiness record was not found.", 404)
            : Response<ScorecardsDashboardsAuditMetadataDto>.Success(ScorecardsDashboardsMapper.ToAuditMetadata(entity));
    }
}
