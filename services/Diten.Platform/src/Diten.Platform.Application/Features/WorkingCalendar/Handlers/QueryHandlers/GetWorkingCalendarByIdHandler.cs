using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkingCalendar.Queries;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.QueryHandlers;

public sealed class GetWorkingCalendarByIdHandler
    : IRequestHandler<GetWorkingCalendarByIdQuery, Response<WorkingCalendarDto>>
{
    private readonly IWorkingCalendarRepository _repository;

    public GetWorkingCalendarByIdHandler(IWorkingCalendarRepository repository) => _repository = repository;

    public async Task<Response<WorkingCalendarDto>> Handle(GetWorkingCalendarByIdQuery request, CancellationToken ct)
    {
        Wc? calendar;
        var readOnly = false;

        if (request.CountryLayer)
        {
            calendar = await _repository.GetCountryLayerByIdAsync(request.Id, ct);
        }
        else
        {
            calendar = await _repository.GetOwnOverrideByIdAsync(request.Id, ct);
            if (calendar is null)
            {
                // The tenant list and QuickView expose ACTIVE country rows as inherited, read-only records. By-id
                // follows the same visibility rule so Details is not a dead link. This does not widen writes:
                // WorkingCalendarWriteGuard still loads tenant mutations exclusively through GetOwnOverrideByIdAsync.
                var inheritedCountry = await _repository.GetCountryLayerByIdAsync(request.Id, ct);
                if (inheritedCountry?.IsActive() == true)
                {
                    calendar = inheritedCountry;
                    readOnly = true;
                }
            }
        }

        if (calendar is null)
        {
            return Response<WorkingCalendarDto>.Fail("Working calendar not found.", 404);
        }

        // For an override, load its country row so the detail view can show the inherited weekend explicitly.
        var countryCalendar = calendar.IsCountryLayer
            ? null
            : (await _repository.GetCountryLayerAsync(calendar.CountryCode, calendar.CalendarYear, ct))
                .FirstOrDefault(c => c.IsActive());

        return Response<WorkingCalendarDto>.Success(
            WorkingCalendarMapper.ToDto(calendar, countryCalendar, readOnly), 200);
    }
}
