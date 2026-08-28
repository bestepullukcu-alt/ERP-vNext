using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Queries;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Handlers.QueryHandlers;

/// <summary>
/// The capacity grid: authored inputs plus the pinned period's identifying fields.
/// <para><b>No estimate is computed here.</b> Every figure would cost one working-calendar HTTP call per month per
/// row, turning a single grid draw into dozens of cross-service calls; the estimate has its own endpoint precisely so
/// a list stays a list.</para>
/// <para>The periods ARE read — in ONE batch through <c>GetByIdsAsync</c>, never one per row (the N+1 a grid makes so
/// easy to write). A period the caller cannot see projects as null rather than hiding the capacity, whose inputs are
/// still the tenant's own.</para>
/// </summary>
public sealed class GetCycleCapacityListHandler
    : IRequestHandler<GetCycleCapacityListQuery, Response<CycleCapacityListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICycleCapacityRepository _capacities;
    private readonly ICyclePeriodReader _periods;

    public GetCycleCapacityListHandler(
        ITenantContext tenant, ICycleCapacityRepository capacities, ICyclePeriodReader periods)
    {
        _tenant = tenant;
        _capacities = capacities;
        _periods = periods;
    }

    public async Task<Response<CycleCapacityListDto>> Handle(
        GetCycleCapacityListQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CycleCapacityListDto>.Fail("Tenant context is required.", 400);
        }

        var rows = await _capacities.ListAsync(tenantId, cancellationToken);

        if (!request.IncludeArchived)
        {
            rows = rows.Where(r => !r.IsArchived).ToList();
        }

        if (request.CyclePeriodId is { } periodId && periodId != Guid.Empty)
        {
            rows = rows.Where(r => r.CyclePeriodId == periodId).ToList();
        }

        if (CycleCapacityValidation.Trim(request.CalendarCountryCode) is { } country)
        {
            rows = rows
                .Where(r => string.Equals(r.CalendarCountryCode, country, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var periods = (await _periods.GetByIdsAsync(
                rows.Select(r => r.CyclePeriodId).Distinct().ToList(), cancellationToken))
            .ToDictionary(p => p.CyclePeriodId);

        var items = rows
            .Select(r => CycleCapacityMapper.ToListItem(
                r, periods.TryGetValue(r.CyclePeriodId, out var period) ? period : null))
            .ToList();

        // The free-text search runs over the PROJECTED period fields as well as the capacity's own, because "cycle 3"
        // is how a user names a capacity — they do not know its GUID. Applied after projection for that reason.
        if (CycleCapacityValidation.Trim(request.Search) is { } search)
        {
            items = items.Where(i => Matches(i, search)).ToList();
        }

        items = items
            .OrderByDescending(i => i.CycleYear ?? 0)
            .ThenBy(i => i.CycleSequenceInYear ?? 0)
            .ThenBy(i => i.CycleCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Response<CycleCapacityListDto>.Success(new CycleCapacityListDto(items, items.Count));
    }

    private static bool Matches(CycleCapacityListItemDto item, string search)
        => Contains(item.CycleCode, search)
           || Contains(item.CycleName, search)
           || Contains(item.CalendarCountryCode, search)
           || Contains(item.CycleScopeRef, search);

    private static bool Contains(string? value, string search)
        => value is not null && value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
