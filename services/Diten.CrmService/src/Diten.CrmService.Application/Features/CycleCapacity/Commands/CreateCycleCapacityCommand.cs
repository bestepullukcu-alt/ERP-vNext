using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Commands;

/// <summary>
/// Creates the capacity model of ONE cycle period. There is no TenantId here — it is resolved server-side from the
/// claim — and no <c>Fte</c>: the interim value comes from configuration, so a caller cannot claim an establishment
/// count the platform cannot verify (D-FTE).
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
    IReadOnlyList<CycleCapacityMonthInput> Months) : IRequest<Response<Guid>>;
