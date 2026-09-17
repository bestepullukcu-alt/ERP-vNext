using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics.Handlers;

public sealed class GetWorkforceAnalyticsReadinessListHandler
    : IRequestHandler<GetWorkforceAnalyticsReadinessListQuery, Response<IReadOnlyList<WorkforceAnalyticsReadinessListItemDto>>>
{
    private readonly IWorkforceAnalyticsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetWorkforceAnalyticsReadinessListHandler(IWorkforceAnalyticsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<WorkforceAnalyticsReadinessListItemDto>>> Handle(
        GetWorkforceAnalyticsReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = WorkforceAnalyticsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<WorkforceAnalyticsReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<WorkforceAnalyticsReadinessListItemDto>>.Success(rows.Select(WorkforceAnalyticsMapper.ToListItem).ToList());
    }
}

public sealed class GetWorkforceAnalyticsReadinessByIdHandler
    : IRequestHandler<GetWorkforceAnalyticsReadinessByIdQuery, Response<WorkforceAnalyticsReadinessDto>>
{
    private readonly IWorkforceAnalyticsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetWorkforceAnalyticsReadinessByIdHandler(IWorkforceAnalyticsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<WorkforceAnalyticsReadinessDto>> Handle(GetWorkforceAnalyticsReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = WorkforceAnalyticsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<WorkforceAnalyticsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<WorkforceAnalyticsReadinessDto>.Fail("WorkforceAnalytics readiness record was not found.", 404)
            : Response<WorkforceAnalyticsReadinessDto>.Success(WorkforceAnalyticsMapper.ToDto(entity));
    }
}

public sealed class GetWorkforceAnalyticsAuditMetadataHandler
    : IRequestHandler<GetWorkforceAnalyticsAuditMetadataQuery, Response<WorkforceAnalyticsAuditMetadataDto>>
{
    private readonly IWorkforceAnalyticsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetWorkforceAnalyticsAuditMetadataHandler(IWorkforceAnalyticsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<WorkforceAnalyticsAuditMetadataDto>> Handle(GetWorkforceAnalyticsAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = WorkforceAnalyticsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<WorkforceAnalyticsAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<WorkforceAnalyticsAuditMetadataDto>.Fail("WorkforceAnalytics readiness record was not found.", 404)
            : Response<WorkforceAnalyticsAuditMetadataDto>.Success(WorkforceAnalyticsMapper.ToAuditMetadata(entity));
    }
}
