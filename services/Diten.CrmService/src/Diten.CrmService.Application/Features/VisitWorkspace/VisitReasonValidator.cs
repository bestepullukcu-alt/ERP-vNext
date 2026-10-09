using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitWorkspace;

/// <summary>
/// WP-VW-W2 (A1) — the ONE check of a cancel / missed / reschedule reason, against the published
/// <see cref="VisitOutcomeReasons.ReasonSet"/> reference set (never a hardcoded list).
/// <list type="number">
/// <item>the code exists and is active in the published set (<see cref="IReferenceDataValidator"/>; inactive values are
/// not selectable) — else 400 <c>visit_reason_invalid</c>;</item>
/// <item>its <c>applies_to</c> attribute lists the action — else 400 <c>visit_reason_invalid</c>;</item>
/// <item>its <c>requires_note</c> attribute is true ⇒ a note is required — else 400 <c>visit_reason_note_required</c>;</item>
/// <item>the set cannot be read ⇒ 503 <c>reference_data_unavailable</c> (fail-closed: we do not know, so nothing is
/// written and the rep is not told their input was wrong).</item>
/// </list>
/// </summary>
public sealed class VisitReasonValidator
{
    public sealed record Failure(string Message, string Code, int StatusCode);

    private readonly IReferenceDataValidator _values;
    private readonly IReferenceMetadataReader _attributes;

    public VisitReasonValidator(IReferenceDataValidator values, IReferenceMetadataReader attributes)
    {
        _values = values;
        _attributes = attributes;
    }

    /// <summary>Validates <paramref name="reasonCode"/> + <paramref name="note"/> for <paramref name="appliesTo"/>
    /// (cancel / missed / reschedule). Null ⇒ valid.</summary>
    public async Task<Failure?> ValidateAsync(
        string? reasonCode, string? note, string appliesTo, CancellationToken cancellationToken)
    {
        var code = Trim(reasonCode);
        if (code is null)
        {
            return new Failure($"A reason is required to {appliesTo} a visit.", VisitWorkspaceErrorCodes.ReasonInvalid, 400);
        }

        var trimmedNote = Trim(note);
        if (trimmedNote is not null && trimmedNote.Length > VisitReportLimits.MaxReasonLength)
        {
            return new Failure(
                $"The note must be at most {VisitReportLimits.MaxReasonLength} characters.",
                VisitWorkspaceErrorCodes.ReasonNoteTooLong, 400);
        }

        var verdict = await _values.ValidateAsync(VisitOutcomeReasons.ReasonSet, code, cancellationToken);
        switch (verdict.Status)
        {
            case ReferenceValidationStatus.SetMissing:
                return Unavailable();
            case ReferenceValidationStatus.InvalidValue:
                return Invalid(code, appliesTo);
        }

        var attributes = await _attributes.GetValueAttributesAsync(VisitOutcomeReasons.ReasonSet, code, cancellationToken);
        if (attributes is null)
        {
            // The value was valid a moment ago; without its attributes we cannot tell what it applies to.
            return Unavailable();
        }

        if (!VisitOutcomeReasons.Applies(attributes, appliesTo))
        {
            return Invalid(code, appliesTo);
        }

        if (VisitOutcomeReasons.RequiresNote(attributes) && trimmedNote is null)
        {
            return new Failure(
                $"The reason '{code}' requires a note.", VisitWorkspaceErrorCodes.ReasonNoteRequired, 400);
        }

        return null;
    }

    /// <summary>The stored form of a reason code.</summary>
    public static string Normalize(string reasonCode) => reasonCode.Trim().ToLowerInvariant();

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Failure Invalid(string code, string appliesTo)
        => new($"'{code}' is not an active '{VisitOutcomeReasons.ReasonSet}' reason for {appliesTo}.",
            VisitWorkspaceErrorCodes.ReasonInvalid, 400);

    private static Failure Unavailable()
        => new($"The reason list ('{VisitOutcomeReasons.ReasonSet}') could not be read. Nothing was saved — please try again.",
            VisitWorkspaceErrorCodes.ReferenceDataUnavailable, 503);
}
