using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Contract;
using Diten.CrmService.Application.Features.CycleCapacity.Queries;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Handlers.QueryHandlers;

/// <summary>
/// Publishes what this FU is and, just as importantly, what it is NOT. The limitations below are the contract a
/// consumer can rely on: they say out loud that the figure is an estimate, that it is never stored, that weekends are
/// already excluded, and that the working calendar belongs to someone else.
/// </summary>
public sealed class GetCycleCapacityContractHandler
    : IRequestHandler<GetCycleCapacityContractQuery, Response<CycleCapacityContractDto>>
{
    public const string ModuleId = "MOD-0155-FU06";
    public const string ModuleName = "Cycle Capacity";
    public const string Service = "Diten.CrmService";

    public const string RuntimeScope =
        "FU06-cycle-capacity (the visit-capacity model of ONE cycle period: an activity minute budget - promoted and "
        + "non-promoted product time per visit, travelling, reporting and quiz time per day - an interim configured "
        + "FTE, and EXPLICIT (year, month) rows carrying meeting / training / vacation day deductions and a monthly "
        + "micro-targeting minute pool; create / read / update / archive, a 1:1 read-only pin to a CyclePeriod, a "
        + "fail-closed READ of the platform working calendar, and a READ-TIME estimate of TotalVisitNumber per month "
        + "and per cycle). It produces NOTHING: no MicroTarget row, no visit, no route, no frequency policy, no "
        + "campaign binding and no working-calendar entry. CyclePeriod (MOD-0165 FU06/FU07) and the platform "
        + "working calendar are READ only and are never written.";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "the number this FU produces is an ESTIMATE, not a quota, a target or a commitment. It is derived from authored average activity times and an INTERIM configured FTE rather than from an HR establishment, and the contract says so through IsEstimate=true rather than leaving it to the screen",
        "the estimate is a READ-TIME PROJECTION and is NEVER persisted: no TotalVisitNumber field exists on the aggregate or on a month row, and no endpoint writes one. Working calendars change - a holiday is published, a tenant override is added - and a stored figure would start lying silently the moment they do. Only the INPUTS are stored",
        "the working-day count already EXCLUDES weekends, public holidays and company closures: it comes from the platform calendar's working-days-between, which walks the range day by day. A consumer must subtract only its OWN deductions (meetings, training, leave); subtracting weekends or holidays again double-counts them",
        "the working calendar is consumed FAIL-CLOSED and READ-ONLY over HTTP. If any month cannot be resolved (no calendar for the country, no calendar for the year, unknown country, or an unreachable dependency) the WHOLE estimate comes back unresolved with a null total and an EMPTY month list - never a partial table and never an invented default month length. null and 0 are different answers: 0 means no time is left for visits, null means the calendar did not speak",
        "a 403 from the working calendar is reported as calendar_forbidden, deliberately NOT as calendar_unresolved: the calendar may exist and the caller simply lacks platform.working-calendar.override.read, which is an RBAC fix rather than a calendar-authoring one (follow-up F-RBAC-WC)",
        "the calendar read never blocks a WRITE. A capacity can be created and edited while the working calendar is unreachable, because the inputs are valid on their own; only the estimate is unavailable. A dependency outage therefore cannot stop a tenant authoring its plan",
        "CalendarCountryCode is a WORKING-CALENDAR QUERY PARAMETER and NOT a scope: it takes no part in the aggregate's identity, its uniqueness or any precedence, and it never overrides where the cycle period lives. It exists because the calendar always needs a country while a tenant-scoped period - the common default - has none to derive. When the period IS country-scoped the code is derived from it server-side and the payload's value is ignored, so the two can never disagree",
        "a business-unit-scoped period does NOT narrow the working calendar. CyclePeriod.BusinessUnitId is a published MOD-0048 value code (a string) while the calendar's organizationUnitId is an organization-unit GUID; coercing one into the other would silently select the wrong calendar. BusinessUnitCountryContext is not used either - its own contract calls it documentation rather than identity, and it is null on older rows (follow-up F-WC-ORG-UNIT)",
        "the pin is 1:1 and IMMUTABLE: one cycle period carries at most one non-archived capacity, and no endpoint can move a capacity to another period - the update command has no CyclePeriodId at all. Redoing a capacity means archiving the old one, which frees the period. Comparing two capacities for the same period is scenario work and is not opened here (follow-up F-SCENARIO)",
        "CyclePeriod is READ-ONLY here and is not modified in any way: no field is added to it, no flag on its contract changes, and its SupportsWorkingCalendarIntegration / SupportsWorkingDayCount stay false - the integration belongs to THIS aggregate, not to the period master. The only CyclePeriod-side change this FU makes anywhere is an additive row-action link in the frontend grid",
        "this aggregate has NO lifecycle of its own and no status field. Whether a capacity may be edited DERIVES from the pinned period: a closed period freezes it (409), a draft or active period does not. A second state machine would be a second source of truth. Approval workflow is not opened (follow-up F-APPROVAL)",
        "the FTE is an INTERIM configured average. It is written by the SERVER at create, is rendered disabled in the UI, and the payload's value is ignored - so re-enabling the field in a browser changes nothing. It is nevertheless STORED, so an old capacity keeps reproducing the same figure after the configured average changes. Per-(business unit, year) granularity, which the legacy model had, is deferred rather than lost (follow-ups F-FTE-HR / F-FTE-BU)",
        "month rows are EXPLICITLY addressed by (Year, MonthNumber). There is no positional twelve-element array, no magic row id and no legacy-system coupling: a period crossing new year's eve is representable here and is not representable in a positional array. Rows are ordered by year and month, never by list position",
        "a month whose deductions exceed its working days is NOT a validation error - the working-day count is unknown at write time - so field days clamp to zero and the month estimates zero visits. That zero is a real answer, and the UI flags the month rather than hiding it",
        "PromoProductTime + NonPromoProductTime must be greater than zero, enforced on the WRITE path so the arithmetic can never divide by zero at read time. Likewise travelling + reporting + quiz time must leave something of the working day",
        "the calculation produces a total for the CYCLE and a figure per MONTH, and distributes neither: which representative visits which account is MOD-0155 FU05 (MicroTarget), and in what order is FU03 (route planning). Comparing the estimate against what actually happened is follow-up F-ACTUALS",
        "capacity answers CAN, not SHOULD. How often a target ought to be visited stays VisitFrequencyPolicy (MOD-0165 FU03) and is neither read nor written here",
        "RBAC keys crm.cycle-capacity.{read,manage} are DEFINED but NOT seeded; the endpoints run on the documented DEV-ONLY territory fallback (follow-up F-RBAC). There is deliberately no .calculate key - the estimate is a view over inputs the reader can already see",
        "there is no DELETE, no PATCH and no bulk-delete endpoint anywhere; retiring a capacity is the soft archive, and TenantId is server-resolved and never accepted from a payload"
    };

    private readonly ITenantContext _tenant;
    private readonly ICycleCapacityDefaultsProvider _defaults;

    public GetCycleCapacityContractHandler(ITenantContext tenant, ICycleCapacityDefaultsProvider defaults)
    {
        _tenant = tenant;
        _defaults = defaults;
    }

    public Task<Response<CycleCapacityContractDto>> Handle(
        GetCycleCapacityContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<CycleCapacityContractDto>.Fail("Tenant context is required.", 400));
        }

        var defaults = _defaults.Current;

        var dto = new CycleCapacityContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true,
            CycleCapacityFeatureFlags.Current,
            CycleCapacityVocabularyDto.Current,
            CycleCapacitySupportedFilters.Current,
            CycleCapacityContractLimits.Current,
            new CycleCapacityDefaultsDto(
                defaults.DailyWorkMinutes,
                defaults.Fte,
                CycleCapacityFteSources.InterimDefault,
                FteIsEditable: false,
                defaults.BetweenVisitTimeMinutes,
                CyclePeriodReferenceSets.CountrySet),
            CycleCapacityReasonCodes.All,
            CycleCapacityPermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<CycleCapacityContractDto>.Success(dto));
    }
}
