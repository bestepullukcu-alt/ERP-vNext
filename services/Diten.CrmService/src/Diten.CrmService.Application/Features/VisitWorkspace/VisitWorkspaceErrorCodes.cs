namespace Diten.CrmService.Application.Features.VisitWorkspace;

/// <summary>WP-VW-W2 — machine-readable refusal codes of the visit workspace rules (cancel / missed / reschedule /
/// unplanned / workspace reads).</summary>
public static class VisitWorkspaceErrorCodes
{
    /// <summary>400 — the reason code is unknown, inactive, or does not apply to this action.</summary>
    public const string ReasonInvalid = "visit_reason_invalid";

    /// <summary>400 — the reason requires a note (requires_note) and none was sent.</summary>
    public const string ReasonNoteRequired = "visit_reason_note_required";

    /// <summary>400 — the note is longer than the published limit.</summary>
    public const string ReasonNoteTooLong = "visit_reason_note_too_long";

    /// <summary>503 — the reason set could not be read (fail-closed: nothing is written).</summary>
    public const string ReferenceDataUnavailable = "reference_data_unavailable";

    /// <summary>409 — a visit whose day is already past is not cancelled (use not done / reschedule).</summary>
    public const string CancelPastDay = "visit_cancel_past_day";

    /// <summary>400 — the reschedule date is not after today, not a working day, or outside the active period (or it is
    /// missing when the rescheduled report is submitted).</summary>
    public const string RescheduleDateInvalid = "visit_reschedule_date_invalid";

    /// <summary>409 — the reschedule already created its new visit; its date can no longer change.</summary>
    public const string RescheduleAlreadyApplied = "visit_reschedule_already_applied";

    /// <summary>400 — an unplanned visit is created only for today (UTC).</summary>
    public const string UnplannedTodayOnly = "unplanned_visit_today_only";

    /// <summary>400 — the appliesTo filter is not cancel / missed / reschedule.</summary>
    public const string AppliesToInvalid = "visit_reason_applies_to_invalid";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ReasonInvalid, ReasonNoteRequired, ReasonNoteTooLong, ReferenceDataUnavailable, CancelPastDay,
        RescheduleDateInvalid, RescheduleAlreadyApplied, UnplannedTodayOnly, AppliesToInvalid
    };
}
