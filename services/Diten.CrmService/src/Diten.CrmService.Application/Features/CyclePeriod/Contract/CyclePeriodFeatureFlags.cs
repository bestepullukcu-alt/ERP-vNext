namespace Diten.CrmService.Application.Features.CyclePeriod.Contract;

/// <summary>
/// What this FU can and cannot do, stated as data. The <c>false</c> flags are deliberate and load-bearing: a period is
/// a period master and nothing else, so every capability a consumer might assume is denied out loud rather than left to
/// be discovered at runtime. Each one names the module that owns it, so nobody has to guess where to go.
/// <para>FU07 widened the SCOPE and nothing else. Every flag FU06 closed stays closed — widening where a period can
/// live is not a licence to widen what it does.</para>
/// </summary>
public sealed record CyclePeriodFeatureFlags(
    bool SupportsCyclePeriod,
    bool SupportsCyclePeriodLifecycle,
    bool SupportsActiveCycleResolution,
    bool SupportsBusinessUnitScopedCycles,
    bool SupportsCountryScopedCycles,
    bool SupportsLegalEntityScopedCycles,
    bool SupportsScopePrecedenceResolution,
    bool SupportsTerritorySourcedBusinessUnits,
    bool SupportsScopeTypeMutation,
    bool SupportsScopeMerge,
    bool SupportsCrossScopeOverlapBan,
    bool SupportsScopeInheritance,
    bool SupportsOrganizationUnitScopedCycles,
    bool SupportsCycleOverlap,
    bool SupportsCycleCalendarHierarchy,
    bool SupportsCyclePeriodVersioning,
    bool SupportsCycleReschedule,
    bool SupportsCycleAutoClose,
    bool SupportsWorkingCalendarIntegration,
    bool SupportsWorkingDayCount,
    bool SupportsMicroTargetGeneration,
    bool SupportsCampaignBinding,
    bool SupportsFrequencyPolicyWrite,
    bool SupportsFrequencyPolicyBackReference,
    bool SupportsStrategyApply,
    bool SupportsHardDelete,
    bool SupportsBulkDelete)
{
    public static CyclePeriodFeatureFlags Current => new(
        SupportsCyclePeriod: true,
        SupportsCyclePeriodLifecycle: true,          // draft → active → closed, no way back
        SupportsActiveCycleResolution: true,         // resolve-active answers 0 or 1 period
        SupportsBusinessUnitScopedCycles: true,      // FU06 level, kept

        // Opened by FU07.
        SupportsCountryScopedCycles: true,           // country is a scope LEVEL, in the identity key
        SupportsLegalEntityScopedCycles: true,       // MDM-proved, fail-closed before persistence
        SupportsScopePrecedenceResolution: true,     // business-unit > legal-entity > country > tenant
        SupportsTerritorySourcedBusinessUnits: true, // candidates derived from MOD-0151 plans (a narrowing, not a gate)

        // Closed by FU07, on purpose.
        SupportsScopeTypeMutation: false,            // scope is identity: close and open a new period instead
        SupportsScopeMerge: false,                   // an answer always comes from exactly ONE level
        SupportsCrossScopeOverlapBan: false,         // periods at DIFFERENT levels may share days - that is what precedence is for
        SupportsScopeInheritance: false,             // there is FALLBACK, not inheritance: ResolvedScopeType says which level actually answered
        SupportsOrganizationUnitScopedCycles: false, // the working calendar has this level; CRM deliberately does not (F-ORG-UNIT-SCOPE)

        // Closed by FU06 - every one of these stays closed.
        SupportsCycleOverlap: false,                 // active periods of ONE scope may never share a day
        SupportsCycleCalendarHierarchy: false,       // VisitFrequencyPolicy.CycleId has NO master (F-CYCLE-CALENDAR)
        SupportsCyclePeriodVersioning: false,        // a period is a calendar fact, not an authored play
        SupportsCycleReschedule: false,              // an active period's dates are immutable (F-RESCHEDULE)
        SupportsCycleAutoClose: false,               // no job, no scheduler: time never mutates a row
        SupportsWorkingCalendarIntegration: false,   // the working calendar is a different question (F-CALENDAR-DAYS)
        SupportsWorkingDayCount: false,              // idem
        SupportsMicroTargetGeneration: false,        // MOD-0155 FU05
        SupportsCampaignBinding: false,              // MOD-0165 FU04 Campaign is untouched (F-CAMPAIGN-BIND)
        SupportsFrequencyPolicyWrite: false,         // MOD-0165 FU03 owns VisitFrequencyPolicy
        SupportsFrequencyPolicyBackReference: false, // VFP.CyclePeriodId is not FK-checked here (F-VFP-FK)
        SupportsStrategyApply: false,                // MOD-0167 FU04 keeps this closed too
        SupportsHardDelete: false,
        SupportsBulkDelete: false);
}
