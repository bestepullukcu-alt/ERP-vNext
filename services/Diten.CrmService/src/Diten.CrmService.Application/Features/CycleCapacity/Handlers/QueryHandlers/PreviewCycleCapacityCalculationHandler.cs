using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Queries;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using MediatR;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Features.CycleCapacity.Handlers.QueryHandlers;

/// <summary>
/// The live estimate the create/edit form calls while an author is typing.
///
/// <para><b>Transient by construction.</b> The capacity it estimates is built in memory from the request and is never
/// handed to a repository — this handler holds no <see cref="Domain.Repositories.ICycleCapacityRepository"/> at all,
/// so it could not persist one even by mistake. It has no <c>Id</c>, and <c>TenantId</c> is set only so the object is
/// internally consistent; nothing reads it back.</para>
///
/// <para><b>Same rule, same number.</b> The month resolution, the fail-closed calendar policy and the arithmetic all
/// come from <see cref="CycleCapacityEstimator"/> and the pure <see cref="Rules.CycleCapacityCalculator"/>, shared with
/// the saved capacity's endpoint. That is the point: a preview that used its own copy of the rule would eventually
/// show a figure the saved record disagrees with, and the author would trust the wrong one.</para>
///
/// <para><b>The FTE is server-stamped here too.</b> The query carries none, so the preview is built on the same
/// configured average the save will store (D-FTE).</para>
///
/// <para>The PERIOD is still read, and read-only: the window it supplies decides which months exist, and a caller
/// cannot invent one.</para>
/// </summary>
public sealed class PreviewCycleCapacityCalculationHandler
    : IRequestHandler<PreviewCycleCapacityCalculationQuery, Response<CycleCapacityCalculationDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICyclePeriodReader _periods;
    private readonly CycleCapacityEstimator _estimator;
    private readonly ICycleCapacityDefaultsProvider _defaults;

    public PreviewCycleCapacityCalculationHandler(
        ITenantContext tenant,
        ICyclePeriodReader periods,
        CycleCapacityEstimator estimator,
        ICycleCapacityDefaultsProvider defaults)
    {
        _tenant = tenant;
        _periods = periods;
        _estimator = estimator;
        _defaults = defaults;
    }

    public async Task<Response<CycleCapacityCalculationDto>> Handle(
        PreviewCycleCapacityCalculationQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CycleCapacityCalculationDto>.Fail("Tenant context is required.", 400);
        }

        // The period is the one thing a preview cannot make up: its window decides which months exist. An id the
        // caller's tenant does not own answers 404, exactly as it would on the write path.
        var period = await _periods.GetByIdAsync(request.CyclePeriodId, cancellationToken);
        if (period is null)
        {
            return Response<CycleCapacityCalculationDto>.Fail(
                new[] { "The cycle period does not exist.", CycleCapacityReasonCodes.PeriodNotFound }, 404);
        }

        var transient = ToTransientCapacity(request, tenantId, _defaults.Current.Fte);
        var estimate = await _estimator.EstimateAsync(transient, period, cancellationToken);

        // Report the country the calendar was actually asked about. A country-scoped period DERIVES its own and
        // ignores the caller's, so echoing the request's value back would show one country while the figures were
        // computed against another. The saved path has nothing to do here: its stored code is already the derived one.
        if (estimate.CalendarCountryCode is { } resolvedCountry)
        {
            transient.CalendarCountryCode = resolvedCountry;
        }

        return CycleCapacityCalculationResponse.From(transient, estimate);
    }

    /// <summary>
    /// The in-memory capacity the estimate runs against. Deliberately NOT the same object shape the write path builds:
    /// no <c>Id</c>, no provenance, no archive flag — nothing that would make it look like a record that exists.
    /// </summary>
    private static CapacityEntity ToTransientCapacity(
        PreviewCycleCapacityCalculationQuery request, Guid tenantId, decimal fte)
        => new()
        {
            // EntityBase seeds a fresh Guid, and a preview must not carry one: an id in the answer is something a
            // caller could mistake for a record that exists, or try to save against.
            Id = Guid.Empty,
            TenantId = tenantId,
            CyclePeriodId = request.CyclePeriodId,
            CalendarCountryCode = (request.CalendarCountryCode ?? string.Empty).Trim().ToUpperInvariant(),
            DailyWorkMinutes = request.DailyWorkMinutes,
            PromoProductTime = request.PromoProductTime,
            NonPromoProductTime = request.NonPromoProductTime,
            TravelingTime = request.TravelingTime,
            ReportDuration = request.ReportDuration,
            QuizDuration = request.QuizDuration,
            Months = request.Months
                .Where(m => m.MonthNumber is >= CycleCapacityLimits.MinMonthNumber
                                          and <= CycleCapacityLimits.MaxMonthNumber)
                .Select(m => new CycleCapacityMonth
                {
                    Year = m.Year,
                    MonthNumber = m.MonthNumber,
                    MeetingDays = Math.Max(0, m.MeetingDays),
                    TrainingDays = Math.Max(0, m.TrainingDays),
                    VacationDays = Math.Max(0, m.VacationDays),
                    MicroTargetingDayCount = Math.Max(0, m.MicroTargetingDayCount),
                    MicroTargetingDuration = Math.Max(0, m.MicroTargetingDuration),
                    // FU07 — the same configured average the SAVE would stamp, on every month. A preview built on a
                    // different FTE would show a figure the saved record then contradicts.
                    Fte = fte,
                    FteSource = CycleCapacityFteSources.InterimDefault
                })
                .ToList()
        };
}
