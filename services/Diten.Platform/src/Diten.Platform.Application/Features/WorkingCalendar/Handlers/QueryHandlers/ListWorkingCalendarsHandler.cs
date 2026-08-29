using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkingCalendar.Queries;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.QueryHandlers;

public sealed class ListWorkingCalendarsHandler
    : IRequestHandler<ListWorkingCalendarsQuery, Response<WorkingCalendarListDto>>
{
    private readonly IWorkingCalendarRepository _repository;

    public ListWorkingCalendarsHandler(IWorkingCalendarRepository repository) => _repository = repository;

    public async Task<Response<WorkingCalendarListDto>> Handle(ListWorkingCalendarsQuery request, CancellationToken ct)
    {
        // The COUNTRY surface stays single-layer: an operator listing country calendars must never receive tenant
        // rows. The TENANT surface is deliberately two-layer (2026-08-27 decision, pack §9.3): it shows the tenant's
        // own overrides PLUS the active country calendars they layer on top of, so an admin can see what they are
        // overriding instead of authoring blind. Public holidays are public information; what stays closed is
        // WRITING them and seeing ANOTHER tenant's rows.
        var ownRows = request.CountryLayer
            ? await _repository.ListCountryLayerAsync(ct)
            : await _repository.ListTenantOverridesAsync(ct);

        // Only ACTIVE country rows are inherited. A draft or archived country calendar resolves to nothing, so
        // showing it on the tenant surface would advertise an inheritance that does not exist.
        // `ListCountryLayerAsync` filters `TenantId == null`, so this can never surface another tenant's data —
        // that is what keeps AC-SEC-3/9 intact while AC-SEC-6 is relaxed.
        var inheritedRows = request.CountryLayer
            ? Array.Empty<Wc>()
            : (await _repository.ListCountryLayerAsync(ct)).Where(c => c.IsActive()).ToArray();

        var readOnlyIds = inheritedRows.Select(c => c.Id).ToHashSet();

        IEnumerable<Wc> query = ownRows.Concat(inheritedRows);

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var country = request.CountryCode.Trim();
            query = query.Where(c => string.Equals(c.CountryCode, country, StringComparison.OrdinalIgnoreCase));
        }

        if (request.CalendarYear is { } year)
        {
            query = query.Where(c => c.CalendarYear == year);
        }

        if (!string.IsNullOrWhiteSpace(request.ScopeType))
        {
            query = query.Where(c => string.Equals(c.ScopeType, request.ScopeType, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(request.CalendarStatus))
        {
            query = query.Where(c => string.Equals(c.CalendarStatus, request.CalendarStatus, StringComparison.Ordinal));
        }

        if (request.OrganizationUnitId is { } ouId && ouId != Guid.Empty)
        {
            query = query.Where(c => c.OrganizationUnitId == ouId);
        }

        if (!request.IncludeArchived)
        {
            query = query.Where(c => !c.IsArchived());
        }

        var list = query
            .OrderByDescending(c => c.CalendarYear)
            .ThenBy(c => c.CountryCode, StringComparer.OrdinalIgnoreCase)
            // The inherited country row sorts ABOVE the overrides that layer on it, so the group reads top-down as
            // "this is the country calendar, and these are my changes to it".
            .ThenByDescending(c => c.IsCountryLayer)
            .ThenBy(c => c.CalendarCode, StringComparer.Ordinal)
            .ToList();

        // Override rows are mapped against their country row so the list can show the weekend they actually resolve
        // to, flagged as inherited — otherwise an inheriting row renders an empty weekend column that reads as
        // "no weekend defined".
        var countryLookup = request.CountryLayer
            ? new Dictionary<(string, int), Wc>()
            : inheritedRows
                .GroupBy(c => (c.CountryCode.ToUpperInvariant(), c.CalendarYear))
                .ToDictionary(g => g.Key, g => g.First());

        var items = list
            .Select(c =>
            {
                countryLookup.TryGetValue((c.CountryCode.ToUpperInvariant(), c.CalendarYear), out var countryCal);
                return WorkingCalendarMapper.ToListItem(c, countryCal, readOnly: readOnlyIds.Contains(c.Id));
            })
            .ToList();

        return Response<WorkingCalendarListDto>.Success(new WorkingCalendarListDto(items.Count, items), 200);
    }
}
