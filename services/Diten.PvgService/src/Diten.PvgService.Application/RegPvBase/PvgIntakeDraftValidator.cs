using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public static class PvgIntakeDraftValidator
{
    private const int SourceReferenceMaxLength = 128;
    private const int ReporterContactSummaryMaxLength = 256;
    private const int PatientSubjectCodeMaxLength = 64;
    private const int AdverseEventNarrativeMaxLength = 8000;
    private const int SuspectProductTextMaxLength = 512;
    private const int TriageReasonMaxLength = 1000;
    private const int EvidenceLinkReferencesMaxCount = 20;
    private static readonly DateTimeOffset EarliestSupportedReceivedAtUtc = new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly EarliestSupportedEventOnsetDate = new(1900, 1, 1);

    private static readonly HashSet<string> SupportedTriageReasonCodes = new(StringComparer.Ordinal)
    {
        "PVG_TRIAGE_REASON_VALID",
        "PVG_TRIAGE_REASON_REJECTED",
        "PVG_TRIAGE_REASON_DUPLICATE"
    };

    public static PvgValidationResult ValidateCreate(PvgServerTenantContext? tenantContext, PvgCreateIntakeDraftRequest request)
    {
        var failures = NewFailureList(tenantContext);
        AddBaselineFieldFailures(
            failures,
            request.IntakeChannel,
            request.SourceType,
            request.ReceivedAtUtc,
            request.ReporterType,
            request.AdverseEventNarrative,
            request.Seriousness,
            request.IntakePriority);
        AddCreateUpdateFieldRuleFailures(
            failures,
            request.SourceReference,
            request.ReceivedAtUtc,
            request.ReporterContactSummary,
            request.PatientSubjectCode,
            request.EventOnsetDate,
            request.AdverseEventNarrative,
            request.SuspectProductText,
            request.EvidenceLinkReferences);

        return ToResult(failures);
    }

    public static PvgValidationResult ValidateUpdate(PvgServerTenantContext? tenantContext, PvgUpdateIntakeDraftRequest request)
    {
        var failures = NewFailureList(tenantContext);
        AddBaselineFieldFailures(
            failures,
            request.IntakeChannel,
            request.SourceType,
            request.ReceivedAtUtc,
            request.ReporterType,
            request.AdverseEventNarrative,
            request.Seriousness,
            request.IntakePriority);
        AddCreateUpdateFieldRuleFailures(
            failures,
            request.SourceReference,
            request.ReceivedAtUtc,
            request.ReporterContactSummary,
            request.PatientSubjectCode,
            request.EventOnsetDate,
            request.AdverseEventNarrative,
            request.SuspectProductText,
            request.EvidenceLinkReferences);

        return ToResult(failures);
    }

    public static PvgValidationResult ValidateTriage(PvgServerTenantContext? tenantContext, PvgTriageIntakeDraftRequest request)
    {
        var failures = NewFailureList(tenantContext);

        if (request.TriageOutcome is null)
        {
            failures.Add(Missing(PvgIntakeField.TriageOutcome));
        }

        if (request.TriageOutcome is PvgTriageOutcome.Rejected or PvgTriageOutcome.Duplicate &&
            IsBlank(request.TriageReasonCode))
        {
            failures.Add(Missing(PvgIntakeField.TriageReason));
        }

        var triageReasonCode = request.TriageReasonCode?.Trim();
        if (triageReasonCode is { Length: > 0 } &&
            !SupportedTriageReasonCodes.Contains(triageReasonCode))
        {
            failures.Add(Invalid(PvgIntakeField.TriageReason));
        }

        if (TrimmedLengthExceeds(request.TriageReason, TriageReasonMaxLength))
        {
            failures.Add(Invalid(PvgIntakeField.TriageReason));
        }

        return ToResult(failures);
    }

    public static PvgValidationResult ValidateRoute(PvgServerTenantContext? tenantContext, PvgRouteIntakeDraftRequest request)
    {
        var failures = NewFailureList(tenantContext);

        if (IsBlank(request.RouteTargetQueue))
        {
            failures.Add(Missing(PvgIntakeField.RouteTargetQueue));
        }

        return ToResult(failures);
    }

    private static List<PvgValidationFailure> NewFailureList(PvgServerTenantContext? tenantContext)
    {
        var failures = new List<PvgValidationFailure>();
        if (tenantContext is null || IsBlank(tenantContext.TenantId))
        {
            failures.Add(new PvgValidationFailure(null, PvgValidationReasonCodes.TenantContextRequired));
        }

        return failures;
    }

    private static void AddBaselineFieldFailures(
        ICollection<PvgValidationFailure> failures,
        string? intakeChannel,
        string? sourceType,
        DateTimeOffset? receivedAtUtc,
        string? reporterType,
        string? adverseEventNarrative,
        string? seriousness,
        string? intakePriority)
    {
        if (IsBlank(intakeChannel))
        {
            failures.Add(Missing(PvgIntakeField.IntakeChannel));
        }

        if (IsBlank(sourceType))
        {
            failures.Add(Missing(PvgIntakeField.SourceType));
        }

        if (receivedAtUtc is null)
        {
            failures.Add(Missing(PvgIntakeField.ReceivedAtUtc));
        }

        if (IsBlank(reporterType))
        {
            failures.Add(Missing(PvgIntakeField.ReporterType));
        }

        if (IsBlank(adverseEventNarrative))
        {
            failures.Add(Missing(PvgIntakeField.AdverseEventNarrative));
        }

        if (IsBlank(seriousness))
        {
            failures.Add(Missing(PvgIntakeField.Seriousness));
        }

        if (IsBlank(intakePriority))
        {
            failures.Add(Missing(PvgIntakeField.IntakePriority));
        }
    }

    private static void AddCreateUpdateFieldRuleFailures(
        ICollection<PvgValidationFailure> failures,
        string? sourceReference,
        DateTimeOffset? receivedAtUtc,
        string? reporterContactSummary,
        string? patientSubjectCode,
        DateOnly? eventOnsetDate,
        string? adverseEventNarrative,
        string? suspectProductText,
        IReadOnlyList<string>? evidenceLinkReferences)
    {
        if (TrimmedLengthExceeds(sourceReference, SourceReferenceMaxLength))
        {
            failures.Add(Invalid(PvgIntakeField.SourceReference));
        }

        if (receivedAtUtc is { } receivedAt && !IsSupportedReceivedAt(receivedAt))
        {
            failures.Add(Invalid(PvgIntakeField.ReceivedAtUtc));
        }

        if (TrimmedLengthExceeds(reporterContactSummary, ReporterContactSummaryMaxLength))
        {
            failures.Add(Invalid(PvgIntakeField.ReporterContactSummary));
        }

        if (!IsSupportedPatientSubjectCode(patientSubjectCode))
        {
            failures.Add(Invalid(PvgIntakeField.PatientSubjectCode));
        }

        if (eventOnsetDate is { } onsetDate && !IsSupportedEventOnsetDate(onsetDate, receivedAtUtc))
        {
            failures.Add(Invalid(PvgIntakeField.EventOnsetDate));
        }

        if (TrimmedLengthExceeds(adverseEventNarrative, AdverseEventNarrativeMaxLength))
        {
            failures.Add(Invalid(PvgIntakeField.AdverseEventNarrative));
        }

        if (TrimmedLengthExceeds(suspectProductText, SuspectProductTextMaxLength))
        {
            failures.Add(Invalid(PvgIntakeField.SuspectProductText));
        }

        if (evidenceLinkReferences is { Count: > EvidenceLinkReferencesMaxCount })
        {
            failures.Add(Invalid(PvgIntakeField.EvidenceLinkReferences));
        }
    }

    private static PvgValidationFailure Missing(PvgIntakeField field) =>
        new(field, PvgValidationReasonCodes.RequiredFieldMissing);

    private static PvgValidationFailure Invalid(PvgIntakeField field) =>
        new(field, PvgValidationReasonCodes.FieldValueInvalid);

    private static PvgValidationResult ToResult(IReadOnlyList<PvgValidationFailure> failures) =>
        failures.Count == 0 ? PvgValidationResult.Valid : new PvgValidationResult(failures);

    private static bool IsSupportedReceivedAt(DateTimeOffset receivedAtUtc)
    {
        var normalized = receivedAtUtc.ToUniversalTime();
        return normalized >= EarliestSupportedReceivedAtUtc &&
            normalized <= DateTimeOffset.UtcNow.AddMinutes(5);
    }

    private static bool IsSupportedEventOnsetDate(DateOnly eventOnsetDate, DateTimeOffset? receivedAtUtc)
    {
        if (eventOnsetDate < EarliestSupportedEventOnsetDate ||
            eventOnsetDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return false;
        }

        return receivedAtUtc is null ||
            eventOnsetDate <= DateOnly.FromDateTime(receivedAtUtc.Value.UtcDateTime);
    }

    private static bool IsSupportedPatientSubjectCode(string? value)
    {
        if (IsBlank(value))
        {
            return true;
        }

        var trimmed = value!.Trim();
        return trimmed.Length <= PatientSubjectCodeMaxLength &&
            !trimmed.Any(char.IsWhiteSpace) &&
            !trimmed.Contains('@', StringComparison.Ordinal) &&
            CountDigitGroups(trimmed) <= 2;
    }

    private static int CountDigitGroups(string value)
    {
        var groups = 0;
        var inDigitGroup = false;
        foreach (var character in value)
        {
            if (char.IsDigit(character))
            {
                if (!inDigitGroup)
                {
                    groups++;
                    inDigitGroup = true;
                }
            }
            else
            {
                inDigitGroup = false;
            }
        }

        return groups;
    }

    private static bool TrimmedLengthExceeds(string? value, int maxLength) =>
        value?.Trim().Length > maxLength;

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
}
