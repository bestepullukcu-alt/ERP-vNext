using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Handlers.QueryHandlers;

/// <summary>Lists plans for the tenant, applying the supported filters in memory (the repository never sorts the date /
/// audit fields at the server — parallel-arrays). Archived rows are hidden unless <c>includeArchived=true</c>.</summary>
public sealed class ListPlannedVisitsHandler : IRequestHandler<ListPlannedVisitsQuery, Response<PlannedVisitListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlannedVisitRepository _repository;

    public ListPlannedVisitsHandler(ITenantContext tenant, IPlannedVisitRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<PlannedVisitListDto>> Handle(
        ListPlannedVisitsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PlannedVisitListDto>.Fail("Tenant context is required.", 400);
        }

        var rows = await _repository.ListAsync(tenantId, cancellationToken);
        IEnumerable<Domain.Entities.PlannedVisit> query = rows;

        if (!request.IncludeArchived)
        {
            query = query.Where(v => !v.IsArchived());
        }

        if (PlannedVisitValidation.Trim(request.ResourceId) is { } resourceId)
        {
            query = query.Where(v => string.Equals(v.Resource.ResourceId, resourceId, StringComparison.Ordinal));
        }

        if (PlannedVisitValidation.Trim(request.TargetType) is { } targetType)
        {
            var t = PlannedVisitTargetType.Normalize(targetType);
            query = query.Where(v => string.Equals(v.TargetType, t, StringComparison.Ordinal));
        }

        if (request.TargetId is { } targetId && targetId != Guid.Empty)
        {
            query = query.Where(v => v.TargetId == targetId);
        }

        if (PlannedVisitValidation.Trim(request.PlanStatus) is { } planStatus)
        {
            var s = PlannedVisitStatus.Normalize(planStatus);
            query = query.Where(v => string.Equals(v.PlanStatus, s, StringComparison.Ordinal));
        }

        if (PlannedVisitValidation.Trim(request.VisitPurpose) is { } purpose)
        {
            var p = PlannedVisitPurpose.Normalize(purpose);
            query = query.Where(v => string.Equals(v.VisitPurpose, p, StringComparison.Ordinal));
        }

        if (request.TerritoryNodeId is { } nodeId && nodeId != Guid.Empty)
        {
            query = query.Where(v => v.TerritoryNodeId == nodeId);
        }

        if (request.CampaignId is { } campaignId && campaignId != Guid.Empty)
        {
            query = query.Where(v => v.CampaignId == campaignId);
        }

        if (PlannedVisitValidation.ParseDate(request.PlannedDateFrom) is { } from)
        {
            query = query.Where(v => v.PlannedDate >= from);
        }

        if (PlannedVisitValidation.ParseDate(request.PlannedDateTo) is { } to)
        {
            query = query.Where(v => v.PlannedDate <= to);
        }

        var items = query.Select(PlannedVisitMapper.ToListItem).ToList();
        return Response<PlannedVisitListDto>.Success(new PlannedVisitListDto(items, items.Count));
    }
}
