using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public static class PvgIntakeDraftValidator
{
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

    private static PvgValidationFailure Missing(PvgIntakeField field) =>
        new(field, PvgValidationReasonCodes.RequiredFieldMissing);

    private static PvgValidationFailure Invalid(PvgIntakeField field) =>
        new(field, PvgValidationReasonCodes.FieldValueInvalid);

    private static PvgValidationResult ToResult(IReadOnlyList<PvgValidationFailure> failures) =>
        failures.Count == 0 ? PvgValidationResult.Valid : new PvgValidationResult(failures);

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
}
