using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Handlers;

public sealed class GetHrKpiAnalyticsReadinessListHandler
    : IRequestHandler<GetHrKpiAnalyticsReadinessListQuery, Response<IReadOnlyList<HrKpiAnalyticsReadinessListItemDto>>>
{
    private readonly IHrKpiAnalyticsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetHrKpiAnalyticsReadinessListHandler(IHrKpiAnalyticsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<HrKpiAnalyticsReadinessListItemDto>>> Handle(
        GetHrKpiAnalyticsReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = HrKpiAnalyticsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<HrKpiAnalyticsReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<HrKpiAnalyticsReadinessListItemDto>>.Success(rows.Select(HrKpiAnalyticsMapper.ToListItem).ToList());
    }
}

public sealed class GetHrKpiAnalyticsReadinessByIdHandler
    : IRequestHandler<GetHrKpiAnalyticsReadinessByIdQuery, Response<HrKpiAnalyticsReadinessDto>>
{
    private readonly IHrKpiAnalyticsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetHrKpiAnalyticsReadinessByIdHandler(IHrKpiAnalyticsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<HrKpiAnalyticsReadinessDto>> Handle(GetHrKpiAnalyticsReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = HrKpiAnalyticsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrKpiAnalyticsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<HrKpiAnalyticsReadinessDto>.Fail("HrKpiAnalytics readiness record was not found.", 404)
            : Response<HrKpiAnalyticsReadinessDto>.Success(HrKpiAnalyticsMapper.ToDto(entity));
    }
}

public sealed class GetHrKpiAnalyticsAuditMetadataHandler
    : IRequestHandler<GetHrKpiAnalyticsAuditMetadataQuery, Response<HrKpiAnalyticsAuditMetadataDto>>
{
    private readonly IHrKpiAnalyticsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetHrKpiAnalyticsAuditMetadataHandler(IHrKpiAnalyticsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<HrKpiAnalyticsAuditMetadataDto>> Handle(GetHrKpiAnalyticsAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = HrKpiAnalyticsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrKpiAnalyticsAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<HrKpiAnalyticsAuditMetadataDto>.Fail("HrKpiAnalytics readiness record was not found.", 404)
            : Response<HrKpiAnalyticsAuditMetadataDto>.Success(HrKpiAnalyticsMapper.ToAuditMetadata(entity));
    }
}
