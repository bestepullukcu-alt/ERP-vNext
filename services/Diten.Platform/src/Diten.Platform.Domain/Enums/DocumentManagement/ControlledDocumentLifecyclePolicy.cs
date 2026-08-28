namespace Diten.Platform.Domain.Enums.DocumentManagement;

/// <summary>
/// MOD-0029-FU08 — pure, side-effect-free transition matrix + operational-use rules for the controlled document SOP
/// lifecycle (GMG-QMS-SOP-0001 §6.2). Kept next to the enum so every layer shares ONE definition (mirrors
/// <see cref="BaselineReleaseStatusExtensions"/>). This governs the DOCUMENT lifecycle on the Document Master Register
/// entry — it is deliberately separate from the MOD-0028 baseline lifecycle. No approval/release-gate logic lives
/// here (that is FU09/FU10); this is only which transitions are structurally valid.
/// </summary>
public static class ControlledDocumentLifecyclePolicy
{
    /// <summary>
    /// SOP §6.2: the current effective version stays IN FORCE while Under revision. Both Effective and UnderRevision
    /// therefore permit routine use of the existing effective version; every other status does not.
    /// </summary>
    public static bool IsOperationallyEffective(this ControlledDocumentLifecycleStatus status) =>
        status is ControlledDocumentLifecycleStatus.Effective or ControlledDocumentLifecycleStatus.UnderRevision;

    /// <summary>Retired is terminal; Superseded/ObsoleteCopy have no forward lifecycle transitions in this engine.</summary>
    public static bool IsTerminal(this ControlledDocumentLifecycleStatus status) =>
        status is ControlledDocumentLifecycleStatus.Retired
            or ControlledDocumentLifecycleStatus.Superseded
            or ControlledDocumentLifecycleStatus.ObsoleteCopy;

    /// <summary>
    /// The set of statuses reachable from <paramref name="from"/>. Note: Suspended → Effective (reinstatement) is
    /// intentionally NOT permitted in FU08 (guarded; a documented reinstatement flow is a later FU). Transitioning
    /// INTO ObsoleteCopy is a point-of-use reconciliation concern (FU17), not a lifecycle action here.
    /// </summary>
    public static IReadOnlySet<ControlledDocumentLifecycleStatus> AllowedTargets(this ControlledDocumentLifecycleStatus from) => from switch
    {
        ControlledDocumentLifecycleStatus.Draft => new HashSet<ControlledDocumentLifecycleStatus>
        {
            ControlledDocumentLifecycleStatus.InReview,
            ControlledDocumentLifecycleStatus.Retired
        },
        ControlledDocumentLifecycleStatus.InReview => new HashSet<ControlledDocumentLifecycleStatus>
        {
            ControlledDocumentLifecycleStatus.Draft,
            ControlledDocumentLifecycleStatus.ApprovedPendingEffective,
            ControlledDocumentLifecycleStatus.Suspended
        },
        ControlledDocumentLifecycleStatus.ApprovedPendingEffective => new HashSet<ControlledDocumentLifecycleStatus>
        {
            ControlledDocumentLifecycleStatus.Effective,
            ControlledDocumentLifecycleStatus.InReview,
            ControlledDocumentLifecycleStatus.Suspended
        },
        ControlledDocumentLifecycleStatus.Effective => new HashSet<ControlledDocumentLifecycleStatus>
        {
            ControlledDocumentLifecycleStatus.UnderRevision,
            ControlledDocumentLifecycleStatus.Suspended,
            ControlledDocumentLifecycleStatus.Retired
        },
        ControlledDocumentLifecycleStatus.UnderRevision => new HashSet<ControlledDocumentLifecycleStatus>
        {
            ControlledDocumentLifecycleStatus.Effective,   // revision abandoned — existing effective remains
            ControlledDocumentLifecycleStatus.Superseded,  // only once a replacement is effective
            // MOD-0029-FU08A: risk during a revision may require suspension (SOP §6.2: the current effective version
            // remains in force "unless the document is suspended"); and a revision may be retired without replacement
            // (e.g. an overdue-review GQD determination). Both keep the FU08 reason requirement and are not terminal→X.
            ControlledDocumentLifecycleStatus.Suspended,
            ControlledDocumentLifecycleStatus.Retired
        },
        ControlledDocumentLifecycleStatus.Suspended => new HashSet<ControlledDocumentLifecycleStatus>
        {
            ControlledDocumentLifecycleStatus.UnderRevision,
            ControlledDocumentLifecycleStatus.Retired
        },
        // Superseded, Retired, ObsoleteCopy are terminal in this engine.
        _ => new HashSet<ControlledDocumentLifecycleStatus>()
    };

    public static bool CanTransition(this ControlledDocumentLifecycleStatus from, ControlledDocumentLifecycleStatus to) =>
        from.AllowedTargets().Contains(to);
}
