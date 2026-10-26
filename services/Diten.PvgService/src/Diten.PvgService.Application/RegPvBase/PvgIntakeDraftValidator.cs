using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public static class PvgIntakeDraftValidator
{
    private const int SourceReferenceMaxLength = 128;
    private const int ReporterContactSummaryMaxLength = 256;
    private const int PatientSubjectCodeMaxLength = 64;
    private const int AdverseEventNarrativeMaxLength = 8000;
    private const int ProductNameAsReportedMaxLength = 512;
    private const int LotNumberMaxLength = 64;
    private const int ReporterTextMaxLength = 128;
    private const int TriageReasonMaxLength = 1000;
    private const int EvidenceLinkReferencesMaxCount = 20;
    private const int ProductLinesMaxCount = 20;
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
            request.EvidenceLinkReferences);
        AddIntakeFieldRuleFailures(
            failures,
            request.ReceivedAtUtc,
            request.AwarenessAtUtc,
            request.CompanyRecipientPersonId,
            request.EventCountry,
            request.ReportType,
            request.ReporterNameOrInitials,
            request.ReporterProfession,
            request.ReporterCountry);
        AddProductLineFailures(failures, request.ProductLines);
        AddReporterSelectionFailures(failures, request.ReporterSelectionDirectory, request.ReporterSelectionId);

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
            request.EvidenceLinkReferences);
        AddIntakeFieldRuleFailures(
            failures,
            request.ReceivedAtUtc,
            request.AwarenessAtUtc,
            request.CompanyRecipientPersonId,
            request.EventCountry,
            request.ReportType,
            request.ReporterNameOrInitials,
            request.ReporterProfession,
            request.ReporterCountry);
        AddProductLineFailures(failures, request.ProductLines);
        AddReporterSelectionFailures(failures, request.ReporterSelectionDirectory, request.ReporterSelectionId);

        if (request.RemoveReporterLink == true &&
            (!IsBlank(request.ReporterSelectionDirectory) || !IsBlank(request.ReporterSelectionId)))
        {
            failures.Add(new PvgValidationFailure(
                PvgIntakeField.RemoveReporterLink,
                PvgValidationReasonCodes.ReporterLinkRemovalWithSelection));
        }

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

        if (evidenceLinkReferences is { Count: > EvidenceLinkReferencesMaxCount })
        {
            failures.Add(Invalid(PvgIntakeField.EvidenceLinkReferences));
        }
    }

    private static void AddIntakeFieldRuleFailures(
        ICollection<PvgValidationFailure> failures,
        DateTimeOffset? receivedAtUtc,
        DateTimeOffset? awarenessAtUtc,
        string? companyRecipientPersonId,
        string? eventCountry,
        string? reportType,
        string? reporterNameOrInitials,
        string? reporterProfession,
        string? reporterCountry)
    {
        if (awarenessAtUtc is null)
        {
            failures.Add(Missing(PvgIntakeField.AwarenessAtUtc));
        }
        else if (!IsSupportedReceivedAt(awarenessAtUtc.Value))
        {
            failures.Add(Invalid(PvgIntakeField.AwarenessAtUtc));
        }
        else if (receivedAtUtc is { } receivedAt && awarenessAtUtc.Value > receivedAt)
        {
            failures.Add(new PvgValidationFailure(
                PvgIntakeField.AwarenessAtUtc,
                PvgValidationReasonCodes.AwarenessAfterReceived));
        }

        // Fail closed while the MOD-0288 read contract is absent: an id that cannot be verified is not stored.
        if (!IsBlank(companyRecipientPersonId))
        {
            failures.Add(ContractUnavailable(PvgIntakeField.CompanyRecipientPersonId));
        }

        // While the country source is unsettled (EK-1) only the PVG "unknown" value passes.
        if (IsBlank(eventCountry))
        {
            failures.Add(Missing(PvgIntakeField.EventCountry));
        }
        else if (!IsUnknownCountry(eventCountry))
        {
            failures.Add(CountrySourceUnsettled(PvgIntakeField.EventCountry));
        }

        if (!IsBlank(reporterCountry) && !IsUnknownCountry(reporterCountry))
        {
            failures.Add(CountrySourceUnsettled(PvgIntakeField.ReporterCountry));
        }

        if (IsBlank(reportType))
        {
            failures.Add(Missing(PvgIntakeField.ReportType));
        }
        else if (!PvgIntakeOptionSets.ReportTypes.Contains(reportType!.Trim()))
        {
            failures.Add(Invalid(PvgIntakeField.ReportType));
        }

        if (TrimmedLengthExceeds(reporterNameOrInitials, ReporterTextMaxLength))
        {
            failures.Add(Invalid(PvgIntakeField.ReporterNameOrInitials));
        }

        if (TrimmedLengthExceeds(reporterProfession, ReporterTextMaxLength))
        {
            failures.Add(Invalid(PvgIntakeField.ReporterProfession));
        }
    }

    private static void AddProductLineFailures(
        List<PvgValidationFailure> failures,
        IReadOnlyList<PvgIntakeProductLineRequest?>? productLines)
    {
        if (productLines is null)
        {
            return;
        }

        if (productLines.Count > ProductLinesMaxCount)
        {
            failures.Add(Invalid(PvgIntakeField.ProductLines));
        }

        var lineFailures = new List<PvgValidationFailure>();
        foreach (var line in productLines)
        {
            if (line is null)
            {
                lineFailures.Add(Invalid(PvgIntakeField.ProductLines));
                continue;
            }

            if (IsBlank(line.ProductRole))
            {
                lineFailures.Add(Missing(PvgIntakeField.ProductRole));
            }
            else if (!PvgIntakeOptionSets.ProductRoles.Contains(line.ProductRole!.Trim()))
            {
                lineFailures.Add(Invalid(PvgIntakeField.ProductRole));
            }

            if (IsBlank(line.ProductNameAsReported))
            {
                lineFailures.Add(Missing(PvgIntakeField.ProductNameAsReported));
            }
            else if (TrimmedLengthExceeds(line.ProductNameAsReported, ProductNameAsReportedMaxLength))
            {
                lineFailures.Add(Invalid(PvgIntakeField.ProductNameAsReported));
            }

            if (TrimmedLengthExceeds(line.LotNumber, LotNumberMaxLength))
            {
                lineFailures.Add(Invalid(PvgIntakeField.LotNumber));
            }

            // Fail closed while the MOD-0290 read contract is absent.
            if (!IsBlank(line.GlobalProductId))
            {
                lineFailures.Add(ContractUnavailable(PvgIntakeField.GlobalProductId));
            }

            if (!IsBlank(line.PresentationGskuId))
            {
                lineFailures.Add(IsBlank(line.GlobalProductId)
                    ? new PvgValidationFailure(
                        PvgIntakeField.PresentationGskuId,
                        PvgValidationReasonCodes.ProductGskuRequiresGlobalProduct)
                    : ContractUnavailable(PvgIntakeField.PresentationGskuId));
            }
        }

        foreach (var failure in lineFailures.Distinct())
        {
            if (!failures.Contains(failure))
            {
                failures.Add(failure);
            }
        }
    }

    private static void AddReporterSelectionFailures(
        ICollection<PvgValidationFailure> failures,
        string? reporterSelectionDirectory,
        string? reporterSelectionId)
    {
        var hasDirectory = !IsBlank(reporterSelectionDirectory);
        var hasId = !IsBlank(reporterSelectionId);
        if (hasDirectory != hasId)
        {
            failures.Add(new PvgValidationFailure(
                hasDirectory ? PvgIntakeField.ReporterSelectionId : PvgIntakeField.ReporterSelectionDirectory,
                PvgValidationReasonCodes.ReporterSelectionIncomplete));
            return;
        }

        if (!hasDirectory)
        {
            return;
        }

        // Fail closed while the MOD-0150 (Crm) and MOD-0288 (PersonDirectory) read contracts are absent.
        failures.Add(PvgIntakeOptionSets.ReporterSelectionDirectories.Contains(reporterSelectionDirectory!.Trim())
            ? ContractUnavailable(PvgIntakeField.ReporterSelectionDirectory)
            : Invalid(PvgIntakeField.ReporterSelectionDirectory));
    }

    private static bool IsUnknownCountry(string? value) =>
        string.Equals(value?.Trim(), PvgIntakeOptionSets.UnknownCountry, StringComparison.Ordinal);

    private static PvgValidationFailure ContractUnavailable(PvgIntakeField field) =>
        new(field, PvgValidationReasonCodes.OwnerReadContractUnavailable);

    private static PvgValidationFailure CountrySourceUnsettled(PvgIntakeField field) =>
        new(field, PvgValidationReasonCodes.CountrySourceUnsettled);

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
