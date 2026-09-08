using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Handlers;

public sealed class GetWorkforcePlanningReadinessListHandler
    : IRequestHandler<GetWorkforcePlanningReadinessListQuery, Response<IReadOnlyList<WorkforcePlanningReadinessListItemDto>>>
{
    private readonly IWorkforcePlanningReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetWorkforcePlanningReadinessListHandler(IWorkforcePlanningReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<WorkforcePlanningReadinessListItemDto>>> Handle(
        GetWorkforcePlanningReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = WorkforcePlanningGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<WorkforcePlanningReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<WorkforcePlanningReadinessListItemDto>>.Success(rows.Select(WorkforcePlanningMapper.ToListItem).ToList());
    }
}

public sealed class GetWorkforcePlanningReadinessByIdHandler
    : IRequestHandler<GetWorkforcePlanningReadinessByIdQuery, Response<WorkforcePlanningReadinessDto>>
{
    private readonly IWorkforcePlanningReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetWorkforcePlanningReadinessByIdHandler(IWorkforcePlanningReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<WorkforcePlanningReadinessDto>> Handle(GetWorkforcePlanningReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = WorkforcePlanningGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<WorkforcePlanningReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<WorkforcePlanningReadinessDto>.Fail("WorkforcePlanning readiness record was not found.", 404)
            : Response<WorkforcePlanningReadinessDto>.Success(WorkforcePlanningMapper.ToDto(entity));
    }
}

public sealed class GetWorkforcePlanningAuditMetadataHandler
    : IRequestHandler<GetWorkforcePlanningAuditMetadataQuery, Response<WorkforcePlanningAuditMetadataDto>>
{
    private readonly IWorkforcePlanningReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetWorkforcePlanningAuditMetadataHandler(IWorkforcePlanningReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<WorkforcePlanningAuditMetadataDto>> Handle(GetWorkforcePlanningAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = WorkforcePlanningGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<WorkforcePlanningAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<WorkforcePlanningAuditMetadataDto>.Fail("WorkforcePlanning readiness record was not found.", 404)
            : Response<WorkforcePlanningAuditMetadataDto>.Success(WorkforcePlanningMapper.ToAuditMetadata(entity));
    }
}
