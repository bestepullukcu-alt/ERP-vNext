namespace Diten.CrmService.Application.Features.VisitReport.Contract;

/// <summary>
/// What this FU can and cannot do, stated as data. The <c>false</c> flags are deliberate and load-bearing (D8): FU02 is
/// an execution RECORDER, not an engine, so every capability a consumer might assume is denied out loud. Each one names
/// the FU that owns it.
/// </summary>
public sealed record VisitReportFeatureFlags(
    bool SupportsVisitExecutionOutcome,
    bool SupportsVisitReport,
    bool SupportsReportLifecycle,
    bool SupportsAppendOnlyAmendment,
    bool SupportsActualContentStage,
    bool SupportsSamples,
    bool SupportsDoctorFeedback,
    bool SupportsFollowUpFlag,
    bool SupportsExecutionCalendar,
    // Closed on purpose (D8) — every one belongs to another FU or a follow-up.
    bool SupportsExecutedMarkerReflection,
    bool SupportsPlanLifecycleMutation,
    bool SupportsContentAutoAdvance,
    bool SupportsPlanGeneration,
    bool SupportsRouteOptimization,
    bool SupportsGpsCheckIn,
    bool SupportsESignature,
    bool SupportsHardDelete,
    bool SupportsBulkDelete)
{
    public static VisitReportFeatureFlags Current => new(
        SupportsVisitExecutionOutcome: true,        // completed / missed / rescheduled (D-EXECUTION-STATUS = A)
        SupportsVisitReport: true,                  // the immutable report aggregate (D-REPORT-PERSISTENCE = A)
        SupportsReportLifecycle: true,              // draft → submitted → amended
        SupportsAppendOnlyAmendment: true,          // D-EDIT-WINDOW — correction after the window is append-only
        SupportsActualContentStage: true,           // D-STAGE-ADVANCE = B — actual StageIndex recorded on the report
        SupportsSamples: true,                      // typed, reference-data-driven (F-RD)
        SupportsDoctorFeedback: true,
        SupportsFollowUpFlag: true,
        SupportsExecutionCalendar: true,            // D-CALENDAR-UI = A — bespoke Day/Week execution calendar

        SupportsExecutedMarkerReflection: false,    // F-EXECUTED-MARKER — FU01 exposes no "executed" transition; the
                                                    // report-side outcome is the SOLE source of truth (see §Notes)
        SupportsPlanLifecycleMutation: false,       // FU01 owns draft/planned/confirmed/cancelled/archived (untouched)
        SupportsContentAutoAdvance: false,          // MOD-0155 FU04 — nextIndex = prior + 1; FU02 records actuals only
        SupportsPlanGeneration: false,              // MOD-0155 FU05
        SupportsRouteOptimization: false,           // MOD-0155 FU03
        SupportsGpsCheckIn: false,                  // deferred / other SoR (MOD-0280)
        SupportsESignature: false,                  // deferred / other SoR (MOD-0280)
        SupportsHardDelete: false,
        SupportsBulkDelete: false);
}
