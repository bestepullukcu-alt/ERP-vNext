using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Commands;

/// <summary>
/// An edit. <c>CyclePeriodId</c> is absent on purpose: the pin is set once and never moved — re-pointing a capacity at
/// another period would silently rewrite what a past estimate was an estimate OF. The FTE travels per month
/// (WP-CAP-MODEL K-5: sent = authored, omitted = the stored authored value or the configured default), and there is no
/// status field because this aggregate has no lifecycle of its own (D-LIFECYCLE).
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
    int? BetweenVisitTimeMinutes = null,
    // WP-SB-3a — products per visit by role; omitted = keep the stored value (a caller that does not know the fields,
    // e.g. today's form, never resets them).
    int? MaxPromoProducts = null,
    int? MaxNonPromoProducts = null,
    // WP-CAP-MODEL (K-1) — the typical visit: all three or none (none = keep the stored model); mixed → 400.
    int? TypicalPromoCount = null,
    int? TypicalNonPromoCount = null,
    int? ReportMinutesPerVisit = null) : IRequest<Response<bool>>;
