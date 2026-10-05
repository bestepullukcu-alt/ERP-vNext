using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Commands;

/// <summary>
/// Creates the capacity model of ONE cycle period. There is no TenantId here — it is resolved server-side from the
/// claim. The FTE travels per month (<see cref="CycleCapacityMonthInput.Fte"/>, WP-CAP-MODEL K-5): sent = authored,
/// omitted = the configured interim average.
/// <para><c>CyclePeriodId</c> is the PIN and is set exactly once. <c>CalendarCountryCode</c> is a working-calendar
/// query parameter, not a scope: when the pinned period is country-scoped the server derives it and ignores whatever
/// arrived here, so the two can never disagree (D-COUNTRY = B).</para>
/// </summary>
public sealed record CreateCycleCapacityCommand(
    Guid CyclePeriodId,
    string? CalendarCountryCode,
    int DailyWorkMinutes,
    int PromoProductTime,
    int NonPromoProductTime,
    int TravelingTime,
    int ReportDuration,
    int QuizDuration,
    string? Description,
    IReadOnlyList<CycleCapacityMonthInput> Months,
    // MOD-0155 FU06B — the between-visit buffer. Nullable and trailing: a caller that omits it takes the server's
    // configured default, so an existing caller compiles unchanged and an absent field is not an error.
    int? BetweenVisitTimeMinutes = null,
    // WP-SB-3a — products per visit by role; omitted = 3 / 3 (DESIGN-SB-3 §3.5).
    int? MaxPromoProducts = null,
    int? MaxNonPromoProducts = null,
    // WP-CAP-MODEL (K-1) — the typical visit: all three or none (none = legacy model).
    int? TypicalPromoCount = null,
    int? TypicalNonPromoCount = null,
    int? ReportMinutesPerVisit = null) : IRequest<Response<Guid>>;
