using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Queries;

/// <summary>
/// MOD-0155 FU06 — the LIVE estimate, from form inputs rather than from a stored row.
///
/// <para>It exists so an author can see what their numbers produce while they are still typing, instead of saving,
/// reading the detail page, and going back to adjust. It is a <b>QUERY</b> in every sense: it creates nothing, stores
/// nothing and returns nothing that could be mistaken for a saved record — there is no id in the answer to save
/// against.</para>
///
/// <para><b>It carries no <c>Fte</c>.</b> Exactly like create and update: the interim configured average is stamped
/// server-side, so the preview is built on the SAME number the save will store. A preview that let the browser choose
/// its own FTE would show a figure the saved record then contradicts.</para>
///
/// <para><b>It is not a shortcut past validation.</b> The write path still enforces the divisor rule, the day budget
/// and the month-window rule; this only answers "what would the arithmetic say". An input the write path would refuse
/// simply produces a preview the author can see is wrong.</para>
/// </summary>
public sealed record PreviewCycleCapacityCalculationQuery(
    Guid CyclePeriodId,
    string? CalendarCountryCode,
    int DailyWorkMinutes,
    int PromoProductTime,
    int NonPromoProductTime,
    int TravelingTime,
    int ReportDuration,
    int QuizDuration,
    IReadOnlyList<CycleCapacityMonthInput> Months) : IRequest<Response<CycleCapacityCalculationDto>>;
