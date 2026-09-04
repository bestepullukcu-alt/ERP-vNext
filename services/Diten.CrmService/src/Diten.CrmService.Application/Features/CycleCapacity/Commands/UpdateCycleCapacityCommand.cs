using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Commands;

/// <summary>
/// An edit. <c>CyclePeriodId</c> is absent on purpose: the pin is set once and never moved — re-pointing a capacity at
/// another period would silently rewrite what a past estimate was an estimate OF. <c>Fte</c> is absent for the same
/// reason it is absent from create (D-FTE), and there is no status field because this aggregate has no lifecycle of
/// its own (D-LIFECYCLE).
/// </summary>
public sealed record UpdateCycleCapacityCommand(
    Guid CycleCapacityId,
    string? CalendarCountryCode,
    int DailyWorkMinutes,
    int PromoProductTime,
    int NonPromoProductTime,
    int TravelingTime,
    int ReportDuration,
    int QuizDuration,
    string? Description,
    IReadOnlyList<CycleCapacityMonthInput> Months,
    int? ExpectedVersion,
    // MOD-0155 FU06B — the between-visit buffer. Nullable and trailing: an omitting caller takes the configured
    // default, so existing positional callers compile unchanged.
    int? BetweenVisitTimeMinutes = null) : IRequest<Response<bool>>;
