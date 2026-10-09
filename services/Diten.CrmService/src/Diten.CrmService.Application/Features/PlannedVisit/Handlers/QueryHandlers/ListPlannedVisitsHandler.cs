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
    private readonly ICallerScope _caller;
    private readonly VisitTargetNameReader _names;
    private readonly IProductNameReader? _productNames;

    public ListPlannedVisitsHandler(
        ITenantContext tenant, IPlannedVisitRepository repository, ICallerScope caller, VisitTargetNameReader names,
        // WP-VP-4G (F4-4) — names for items older plans stored without (one bulk MDM read for the page, fail-open).
        IProductNameReader? productNames = null)
    {
        _productNames = productNames;
        _tenant = tenant;
        _repository = repository;
        _caller = caller;
        _names = names;
    }

    public async Task<Response<PlannedVisitListDto>> Handle(
        ListPlannedVisitsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PlannedVisitListDto>.Fail("Tenant context is required.", 400);
        }

        var rows = await _repository.ListAsync(tenantId, cancellationToken);
        // WP-VP-2 (B-1) — without crm.planned-visit.read-all the caller sees only plans whose resource is themselves.
        IEnumerable<Domain.Entities.PlannedVisit> query = rows
            .Where(v => _caller.MayAccess(PlannedVisitPermissions.ReadAll, v.Resource.ResourceId));

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

        // WP-VP-2 (B-8) — names for the whole page in one read per master.
        var page = query.ToList();
        var names = await _names.ReadAsync(
            tenantId,
            page.Select(v => v.AccountId).Concat(page.Where(v => VisitTargetNameReader.NamedByInstitution(v.TargetType)).Select(v => (Guid?)v.TargetId)),
            page.Select(v => v.ContactId),
            cancellationToken);
        var items = page.Select(v => PlannedVisitMapper.ToListItem(v, names)).ToList();
        var unnamed = PlannedVisitMapper.UnnamedProductIds(page).Distinct().ToList();
        if (_productNames is not null && unnamed.Count > 0)
        {
            var productNames = await _productNames.ReadNamesAsync(unnamed, cancellationToken);
            items = items.Select(i => i with { ContentItems = PlannedVisitMapper.WithProductNames(i.ContentItems, productNames) }).ToList();
        }
        return Response<PlannedVisitListDto>.Success(new PlannedVisitListDto(items, items.Count));
    }
}
