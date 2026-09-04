using System.Globalization;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitReport;

/// <summary>
/// MOD-0155 FU02 shared write-path validation. Kept in ONE place so record-outcome / submit / amend can never drift
/// apart. Everything here is <b>structural and in-domain</b> and performs <b>no I/O</b>: the cross-aggregate checks (the
/// plan atom exists, the 1:1 guard) need other rows and therefore live in the handlers.
/// <para>ExecutionOutcome and ReportStatus are fail-closed IN-DOMAIN vocabularies (out-of-set → 400). Outcome codes and
/// sample/material types are REFERENCE-DATA-driven (F-RD): they are validated as bounded, non-empty strings — never
/// against a hardcoded fallback list, which would be a silently-drifting second source of truth.</para>
/// </summary>
public static class VisitReportValidation
{
    /// <summary>A rejected write: a message for the human, a machine code for the UI/smoke script, and the status the
    /// handler answers with. Nested so this file declares a single top-level public type.</summary>
    public sealed record Failure(string Message, string? Code, int StatusCode = 400);

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static IReadOnlyList<string> ToErrors(Failure failure)
        => failure.Code is null ? new[] { failure.Message } : new[] { failure.Message, failure.Code };

    /// <summary>Parses an ISO "yyyy-MM-dd" (or a full date-time) into a <see cref="DateOnly"/>. Null on unparseable input.</summary>
    public static DateOnly? ParseDate(string? value)
    {
        var v = Trim(value);
        if (v is null)
        {
            return null;
        }

        if (DateOnly.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
            ? DateOnly.FromDateTime(dto.UtcDateTime)
            : null;
    }

    public static DateTimeOffset? ParseInstant(string? value)
    {
        var v = Trim(value);
        return v is not null
               && DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
            ? dto
            : null;
    }

    /// <summary>Fail-closed vocabulary check. An out-of-set value is refused (400) rather than quietly ignored.</summary>
    public static Failure? ValidateVocabulary(string fieldName, string? value, IReadOnlyList<string> allowed)
    {
        var v = Trim(value);
        if (v is null)
        {
            return new Failure($"{fieldName} is required.", VisitReportErrorCodes.UnsupportedVocabularyValue);
        }

        return allowed.Contains(v.ToLowerInvariant(), StringComparer.Ordinal)
            ? null
            : new Failure(
                $"Unsupported {fieldName} '{v}'. Known values: {string.Join(", ", allowed)}.",
                VisitReportErrorCodes.UnsupportedVocabularyValue);
    }

    public static Failure? ValidateFreeText(string fieldName, string? value, int maxLength)
    {
        var v = Trim(value);
        if (v is null)
        {
            return null;
        }

        return v.Length <= maxLength
            ? null
            : new Failure($"{fieldName} must be at most {maxLength} characters.", VisitReportErrorCodes.FreeTextTooLong);
    }

    public static Failure? ValidateResourceId(string? resourceId)
    {
        var v = Trim(resourceId);
        if (v is null)
        {
            return new Failure("ReportedByResourceId is required.", VisitReportErrorCodes.ResourceRequired);
        }

        return v.Length <= VisitReportLimits.MaxResourceIdLength
            ? null
            : new Failure(
                $"ReportedByResourceId must be at most {VisitReportLimits.MaxResourceIdLength} characters.",
                VisitReportErrorCodes.ResourceRequired);
    }

    /// <summary>The outcome (in-domain fail-closed) plus its reason-code rule: a <c>missed</c>/<c>rescheduled</c> outcome
    /// requires an in-domain reason code (§4.1 ③); <c>completed</c> forbids one.</summary>
    public static Failure? ValidateOutcome(string? outcome, string? reasonCode)
    {
        var trimmed = Trim(outcome);
        if (trimmed is null)
        {
            return new Failure("ExecutionOutcome is required.", VisitReportErrorCodes.OutcomeRequired);
        }

        if (!VisitExecutionOutcome.IsKnown(trimmed))
        {
            return new Failure(
                $"Unsupported ExecutionOutcome '{trimmed}'. Known values: {string.Join(", ", VisitExecutionOutcome.All)}.",
                VisitReportErrorCodes.UnsupportedVocabularyValue);
        }

        var normalized = VisitExecutionOutcome.Normalize(outcome);
        var reason = Trim(reasonCode);

        if (string.Equals(normalized, VisitExecutionOutcome.Completed, StringComparison.Ordinal))
        {
            return null; // a completed visit carries no missed/rescheduled reason code
        }

        // missed / rescheduled → a reason code is required and must be in the in-domain set.
        if (reason is null)
        {
            return new Failure(
                $"A reason code is required for a '{normalized}' outcome.", VisitReportErrorCodes.ReasonCodeRequired);
        }

        return VisitReportReasonCodes.IsKnown(reason)
            ? null
            : new Failure(
                $"Unsupported reason code '{reason}'. Known values: {string.Join(", ", VisitReportReasonCodes.All)}.",
                VisitReportErrorCodes.UnsupportedVocabularyValue);
    }

    /// <summary>The report-content shape for a completed visit: a non-empty outcome code (ref-data, bounded) and
    /// structurally valid samples/actuals. Free text is length-bounded only.</summary>
    public static Failure? ValidateReportContent(
        VisitReportContentActualsInput? content,
        IReadOnlyList<VisitReportSampleInput>? samples,
        VisitReportFeedbackInput? feedback)
    {
        if (feedback is null || Trim(feedback.OutcomeCode) is null)
        {
            return new Failure(
                "An outcome code is required on a completed visit's report.", VisitReportErrorCodes.OutcomeCodeRequired);
        }

        if (Trim(feedback.OutcomeCode)!.Length > VisitReportLimits.MaxOutcomeCodeLength)
        {
            return new Failure(
                $"OutcomeCode must be at most {VisitReportLimits.MaxOutcomeCodeLength} characters.",
                VisitReportErrorCodes.OutcomeCodeRequired);
        }

        if (ValidateFreeText("DoctorFeedback", feedback.DoctorFeedback, VisitReportLimits.MaxFeedbackLength)
            is { } feedbackFailure)
        {
            return feedbackFailure;
        }

        if (ValidateFreeText("FollowUpNotes", feedback.FollowUpNotes, VisitReportLimits.MaxNotesLength)
            is { } followUpFailure)
        {
            return followUpFailure;
        }

        if (content is not null)
        {
            if (ValidateFreeText("StageCode", content.StageCode, VisitReportLimits.MaxStageCodeLength)
                is { } stageCodeFailure)
            {
                return stageCodeFailure with { Code = VisitReportErrorCodes.ContentActualsInvalid };
            }

            if (content.StageIndex is { } idx && idx < 0)
            {
                return new Failure(
                    "Actual StageIndex cannot be negative.", VisitReportErrorCodes.ContentActualsInvalid);
            }
        }

        return ValidateSamples(samples);
    }

    /// <summary>An amendment's optional corrections: content and feedback are both optional, but any provided block must
    /// be structurally valid (a provided feedback still needs a non-empty outcome code; samples are always structural).</summary>
    public static Failure? ValidateAmendmentContent(
        VisitReportContentActualsInput? content,
        IReadOnlyList<VisitReportSampleInput>? samples,
        VisitReportFeedbackInput? feedback)
    {
        if (content is not null)
        {
            if (ValidateFreeText("StageCode", content.StageCode, VisitReportLimits.MaxStageCodeLength)
                is { } stageCodeFailure)
            {
                return stageCodeFailure with { Code = VisitReportErrorCodes.ContentActualsInvalid };
            }

            if (content.StageIndex is { } idx && idx < 0)
            {
                return new Failure(
                    "Actual StageIndex cannot be negative.", VisitReportErrorCodes.ContentActualsInvalid);
            }
        }

        if (feedback is not null)
        {
            return ValidateReportContent(content, samples, feedback);
        }

        return ValidateSamples(samples);
    }

    public static Failure? ValidateSamples(IReadOnlyList<VisitReportSampleInput>? samples)
    {
        if (samples is null || samples.Count == 0)
        {
            return null;
        }

        if (samples.Count > VisitReportLimits.MaxSamples)
        {
            return new Failure(
                $"At most {VisitReportLimits.MaxSamples} samples may be recorded.", VisitReportErrorCodes.SampleInvalid);
        }

        foreach (var sample in samples)
        {
            var itemType = Trim(sample.ItemType);
            if (itemType is null)
            {
                return new Failure("Each sample requires an item type.", VisitReportErrorCodes.SampleInvalid);
            }

            if (itemType.Length > VisitReportLimits.MaxSampleItemTypeLength)
            {
                return new Failure(
                    $"A sample item type must be at most {VisitReportLimits.MaxSampleItemTypeLength} characters.",
                    VisitReportErrorCodes.SampleInvalid);
            }

            if (sample.Quantity < VisitReportLimits.MinSampleQuantity
                || sample.Quantity > VisitReportLimits.MaxSampleQuantity)
            {
                return new Failure(
                    $"A sample quantity must be between {VisitReportLimits.MinSampleQuantity} and "
                    + $"{VisitReportLimits.MaxSampleQuantity}.",
                    VisitReportErrorCodes.SampleInvalid);
            }

            if (ValidateFreeText("Sample notes", sample.Notes, VisitReportLimits.MaxNotesLength) is { } notesFailure)
            {
                return notesFailure with { Code = VisitReportErrorCodes.SampleInvalid };
            }
        }

        return null;
    }
}
