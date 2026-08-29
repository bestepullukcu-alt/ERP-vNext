using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkingCalendar.Provider;
using Diten.Platform.Application.Features.WorkingCalendar.Queries;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.QueryHandlers;

/// <summary>
/// The HTTP face of the provider. It only dispatches — every rule lives in the provider and the resolve engine, so
/// the endpoint and an in-process consumer can never disagree about what a date means.
/// </summary>
public sealed class ResolveWorkingDayHandler
    : IRequestHandler<ResolveWorkingDayQuery, Response<WorkingDayResolveDto>>
{
    private readonly IWorkingCalendarProvider _provider;

    public ResolveWorkingDayHandler(IWorkingCalendarProvider provider) => _provider = provider;

    public async Task<Response<WorkingDayResolveDto>> Handle(ResolveWorkingDayQuery request, CancellationToken ct)
    {
        var operation = (request.Operation ?? string.Empty).Trim();
        if (!WorkingCalendarOperations.IsValid(operation))
        {
            return Response<WorkingDayResolveDto>.Fail(
                $"Unsupported operation '{operation}'. Supported: {string.Join(", ", WorkingCalendarOperations.All)}.", 400);
        }

        var scope = new WorkingCalendarScope(request.CountryCode, request.OrganizationUnitId, request.LegalEntityId);

        switch (operation)
        {
            case WorkingCalendarOperations.IsWorkingDay:
            {
                var result = await _provider.IsWorkingDayAsync(request.Date, scope, ct);
                return Ok(WorkingCalendarMapper.ToResolveDto(operation, result), request);
            }

            case WorkingCalendarOperations.IsHoliday:
            {
                var result = await _provider.GetHolidayAsync(request.Date, scope, ct);
                return Ok(WorkingCalendarMapper.ToResolveDto(operation, result), request);
            }

            case WorkingCalendarOperations.NextWorkingDay:
            {
                var result = await _provider.NextWorkingDayAsync(request.Date, scope, ct);
                return Ok(WorkingCalendarMapper.ToResolveDto(operation, result), request);
            }

            case WorkingCalendarOperations.AddWorkingDays:
            {
                if (request.Days is null)
                {
                    return Response<WorkingDayResolveDto>.Fail("'days' is required for add-working-days.", 400);
                }

                var result = await _provider.AddWorkingDaysAsync(request.Date, request.Days.Value, scope, ct);
                return Ok(WorkingCalendarMapper.ToResolveDto(operation, result), request);
            }

            default:
            {
                if (request.ToDate is null)
                {
                    return Response<WorkingDayResolveDto>.Fail("'toDate' is required for working-days-between.", 400);
                }

                var result = await _provider.WorkingDaysBetweenAsync(request.Date, request.ToDate.Value, scope, ct);

                // A malformed range is a request error, not a calendar outcome — the provider reports it as a value
                // (it must never throw into an in-process consumer) and the HTTP layer turns it into a 400.
                if (result.Resolution == WorkingCalendarResolution.InvalidRange)
                {
                    return Response<WorkingDayResolveDto>.Fail(result.SelectionReason, 400);
                }

                return Ok(WorkingCalendarMapper.ToResolveDto(operation, result), request);
            }
        }
    }

    /// <summary>
    /// An unresolved answer is still a successful 200 — "no calendar has been entered for this country/year" is a
    /// legitimate, actionable answer, not a server error. The consumer is expected to branch on Resolution.
    /// </summary>
    private static Response<WorkingDayResolveDto> Ok(WorkingDayResolveDto dto, ResolveWorkingDayQuery request)
        => Response<WorkingDayResolveDto>.Success(dto with
        {
            OrganizationUnitId = request.OrganizationUnitId,
            LegalEntityId = request.LegalEntityId
        }, 200);
}
