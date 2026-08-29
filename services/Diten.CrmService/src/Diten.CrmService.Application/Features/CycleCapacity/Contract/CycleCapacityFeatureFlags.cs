namespace Diten.CrmService.Application.Features.CycleCapacity.Contract;

/// <summary>
/// What this FU can and cannot do, stated as data. The <c>false</c> flags are deliberate and load-bearing: a capacity
/// is an ESTIMATE of how many visits fit in a period, and every capability a consumer might assume is denied out loud
/// rather than left to be discovered at runtime. Each one names the module that owns it.
/// <para><b>Note what is NOT here.</b> These flags describe <c>CycleCapacity</c>. The <c>CyclePeriod</c> contract's
/// own <c>SupportsWorkingCalendarIntegration</c> and <c>SupportsWorkingDayCount</c> stay <c>false</c> and are not
/// touched by this FU — the integration belongs to this aggregate, not to the period master.</para>
/// </summary>
public sealed record CycleCapacityFeatureFlags(
    bool SupportsCycleCapacity,
    bool SupportsCyclePeriodPin,
    bool SupportsExplicitMonthRows,
    bool SupportsWorkingCalendarConsumption,
    bool SupportsFailClosedCalendar,
    bool SupportsCalendarCountryParameter,
    bool SupportsLegalEntityCalendarNarrowing,
    bool SupportsArchive,
    bool IsEstimate,

    // ── Closed on purpose ─────────────────────────────────────────────────────────────────────────────────────────
    bool SupportsComputedValuePersistence,
    bool SupportsMultipleCapacitiesPerPeriod,
    bool SupportsCyclePeriodMutation,
    bool SupportsWorkingCalendarWrite,
    bool SupportsOrganizationUnitCalendarNarrowing,
    bool SupportsBusinessUnitCalendarNarrowing,
    bool SupportsMicroTargetGeneration,
    bool SupportsVisitDistribution,
    bool SupportsRoutePlanning,
    bool SupportsFrequencyPolicyWrite,
    bool SupportsCampaignBinding,
    bool SupportsHrFteIntegration,
    bool SupportsPerBusinessUnitFte,
    bool SupportsCapacityApproval,
    bool SupportsCapacityLifecycle,
    bool SupportsScenarioComparison,
    bool SupportsActualsComparison,
    bool SupportsHardDelete,
    bool SupportsBulkDelete)
{
    public static CycleCapacityFeatureFlags Current => new(
        SupportsCycleCapacity: true,
        SupportsCyclePeriodPin: true,                       // read-only pin, proved before every write
        SupportsExplicitMonthRows: true,                    // (Year, MonthNumber) — never a positional array
        SupportsWorkingCalendarConsumption: true,           // READ only, over HTTP, no cache
        SupportsFailClosedCalendar: true,                   // no calendar ⇒ no number; never a default month length
        SupportsCalendarCountryParameter: true,             // D-COUNTRY = B: a query parameter, NOT a scope
        SupportsLegalEntityCalendarNarrowing: true,         // free precision from a legal-entity-scoped period
        SupportsArchive: true,
        IsEstimate: true,                                   // stated in the contract, not only on the screen

        // Closed by this FU, on purpose.
        SupportsComputedValuePersistence: false,            // the figure is a read-time projection, never stored
        SupportsMultipleCapacitiesPerPeriod: false,         // 1:1 — scenarios are F-SCENARIO
        SupportsCyclePeriodMutation: false,                 // MOD-0165 FU06/FU07 owns the period; nothing is written
        SupportsWorkingCalendarWrite: false,                // CAND-CAP-0008 owns the calendar
        SupportsOrganizationUnitCalendarNarrowing: false,   // CRM has no organization-unit scope (F-WC-ORG-UNIT)
        SupportsBusinessUnitCalendarNarrowing: false,       // a value code is not an org-unit id (F-WC-ORG-UNIT)
        SupportsMicroTargetGeneration: false,               // MOD-0155 FU05
        SupportsVisitDistribution: false,                   // capacity is a total, not an allocation
        SupportsRoutePlanning: false,                       // MOD-0155 FU03
        SupportsFrequencyPolicyWrite: false,                // MOD-0165 FU03 — "can" is not "should"
        SupportsCampaignBinding: false,                     // MOD-0165 FU04/FU08
        SupportsHrFteIntegration: false,                    // no HR master exists yet (F-FTE-HR)
        SupportsPerBusinessUnitFte: false,                  // legacy granularity deferred (F-FTE-BU)
        SupportsCapacityApproval: false,                    // F-APPROVAL — an estimate is not approved
        SupportsCapacityLifecycle: false,                   // editability derives from the pinned period's status
        SupportsScenarioComparison: false,                  // F-SCENARIO
        SupportsActualsComparison: false,                   // F-ACTUALS
        SupportsHardDelete: false,
        SupportsBulkDelete: false);
}
