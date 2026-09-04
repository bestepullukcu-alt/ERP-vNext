namespace Diten.CrmService.Application.Features.PlannedVisit.Contract;

/// <summary>
/// What this FU can and cannot do, stated as data. The <c>false</c> flags are deliberate and load-bearing (D8): a
/// PlannedVisit is a planning FOUNDATION, not an engine, so every capability a consumer might assume is denied out loud
/// rather than left to be discovered at runtime. Each one names the FU that owns it.
/// </summary>
public sealed record PlannedVisitFeatureFlags(
    bool SupportsPlannedVisit,
    bool SupportsPlannedVisitLifecycle,
    bool SupportsPharmacyTarget,
    bool SupportsFrequencyProvenance,
    bool SupportsConsentProvenance,
    bool SupportsJourneyBinding,
    bool SupportsContentPositionStorage,
    bool SupportsSelectionProvenance,
    bool SupportsAvailabilitySnapshot,
    bool SupportsDurationOverride,
    // Closed on purpose (D8) — every one belongs to a later FU or follow-up.
    bool SupportsRouteOptimization,
    bool SupportsSlotPacking,
    bool SupportsPlanGeneration,
    bool SupportsContentAutoAdvance,
    bool SupportsDurationComputation,
    bool SupportsVisitExecution,
    bool SupportsVisitReport,
    bool SupportsMicroTarget,
    bool SupportsAvailabilityHardConstraint,
    bool SupportsHardDelete,
    bool SupportsBulkDelete)
{
    public static PlannedVisitFeatureFlags Current => new(
        SupportsPlannedVisit: true,
        SupportsPlannedVisitLifecycle: true,        // draft → planned → confirmed → cancelled → archived
        SupportsPharmacyTarget: true,               // D9 - a pharmacy is a first-class Account target
        SupportsFrequencyProvenance: true,          // MOD-0165 resolver read, stored as provenance (D5)
        SupportsConsentProvenance: true,            // MOD-0164 evaluator read, fail-closed at confirm (D6)
        SupportsJourneyBinding: true,               // MOD-0162 FU05 optional journey/stage binding
        SupportsContentPositionStorage: true,       // D10 - derive-default + manual override, STORED not advanced
        SupportsSelectionProvenance: true,          // D11 - selection origin snapshot, always manual in FU01
        SupportsAvailabilitySnapshot: true,         // D13 - per-contact snapshot, a WARNING in FU01
        SupportsDurationOverride: true,             // D14 - stored manual override only

        SupportsRouteOptimization: false,           // MOD-0155 FU03
        SupportsSlotPacking: false,                 // MOD-0155 FU05 - Slot.* is null-born storage only
        SupportsPlanGeneration: false,              // no auto plan generation from frequency (D8)
        SupportsContentAutoAdvance: false,          // MOD-0155 FU04 - StageIndex is never advanced here (D10)
        SupportsDurationComputation: false,         // MOD-0155 FU05 - duration is stored, not computed (D14)
        SupportsVisitExecution: false,              // MOD-0155 FU02 - check-in/GPS/actuals
        SupportsVisitReport: false,                 // MOD-0155 FU02
        SupportsMicroTarget: false,                 // MOD-0155 FU05
        SupportsAvailabilityHardConstraint: false,  // MOD-0155 FU05 - hard constraint + override lives there (D13)
        SupportsHardDelete: false,
        SupportsBulkDelete: false);
}
