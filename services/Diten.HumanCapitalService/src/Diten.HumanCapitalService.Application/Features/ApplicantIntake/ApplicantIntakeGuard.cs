using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using System.Text.RegularExpressions;

namespace Diten.HumanCapitalService.Application.Features.ApplicantIntake;

public static class ApplicantIntakeGuard
{
    public const string OwnerKey = "hcm.applicant-intake";
    public const string ReadPermission = "hcm.applicant-intake.read";
    public const string ManagePermission = "hcm.applicant-intake.manage";
    public const string EvaluatePermission = "hcm.applicant-intake.evaluate";
    public const string AuditReadPermission = "hcm.applicant-intake.audit.read";

    private static readonly string[] ForbiddenMarkers =
    [
        "resume",
        "cv",
        "coverletter",
        "cover_letter",
        "narrative",
        "freetext",
        "free_text",
        "attachment",
        "documentpayload",
        "document_payload",
        "rawpayload",
        "raw_payload",
        "providerpayload",
        "provider_payload",
        "credential",
        "access_token",
        "refresh_token",
        "secret",
        "password",
        "payroll",
        "bank",
        "tax",
        "payslip",
        "biometric",
        "geolocation",
        "nationalid",
        "national_id",
        "dateofbirth",
        "dob",
        "homeaddress",
        "home_address",
        "salary",
        "wage",
        "score",
        "rank",
        "modeloutput",
        "model_output",
        "automateddecision",
        "automated_decision"
    ];

    // Word/token-boundary matcher: markers only match as standalone tokens, so legitimate
    // words such as "taxonomy" (contains "tax") or "scorecard" (contains "score") are NOT
    // falsely rejected, while real markers ("tax", "ssn", "salary", ...) still match. Tested
    // against the RAW value. Underscore is a regex word character, so snake_case markers match.
    private static readonly Regex ForbiddenMarkerRegex = new(
        @"\b(" + string.Join("|", ForbiddenMarkers.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> Validate(ApplicantIntakeCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.ApplicantIntakeVersion < 1)
        {
            errors.Add("ApplicantIntakeVersion must be greater than zero.");
        }

        ValidateState(request.IntakeState, nameof(request.IntakeState), errors);
        ValidateState(request.SourceChannelState, nameof(request.SourceChannelState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.DuplicateHandlingState, nameof(request.DuplicateHandlingState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);
        ValidateState(request.ApplicantIdentityBoundaryState, nameof(request.ApplicantIdentityBoundaryState), errors);
        ValidateState(request.PublicUxBoundaryState, nameof(request.PublicUxBoundaryState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);

        if (request.IntakeState == ApplicantIntakeReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.PublicUxBoundaryState == ApplicantIntakeReadinessState.Ready)
        {
            errors.Add("Public application UX cannot be marked Ready in the first metadata-only slice.");
        }

        foreach (var pair in request.DependencyStates)
        {
            RequireText(pair.Key, nameof(request.DependencyStates), 80, errors);
            ValidateState(pair.Value, $"{nameof(request.DependencyStates)}.{pair.Key}", errors);
        }

        if (ForbiddenValues(request).Any(ContainsForbiddenMarker))
        {
            errors.Add("Applicant intake metadata cannot contain resume/CV, cover letter, free-text, attachment, raw provider payload, credential, payroll, bank, tax, biometric, geolocation, national ID, DOB, home address, scoring, ranking, automated decision, or PII-heavy markers.");
        }

        return errors;
    }

    public static ApplicantIntakeReadinessState ResolveFailClosedIntakeState(ApplicantIntakeCreateRequest request)
    {
        if (request.IntakeState != ApplicantIntakeReadinessState.Ready)
        {
            return request.IntakeState;
        }

        return ArePreconditionsReady(
            request.SourceChannelState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.DuplicateHandlingState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.ApplicantIdentityBoundaryState,
            request.PublicUxBoundaryState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.DependencyStates)
            ? ApplicantIntakeReadinessState.Ready
            : ApplicantIntakeReadinessState.Deferred;
    }

    public static void ApplyEvaluation(ApplicantIntakeReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.IntakeState = ArePreconditionsReady(
            entity.SourceChannelState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.DuplicateHandlingState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.ApplicantIdentityBoundaryState,
            entity.PublicUxBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.DependencyStates)
            ? ApplicantIntakeReadinessState.Ready
            : ApplicantIntakeReadinessState.Deferred;

        entity.DeferredReason = entity.IntakeState == ApplicantIntakeReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Applicant intake readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        ApplicantIntakeReadinessState sourceChannel,
        ApplicantIntakeReadinessState consent,
        ApplicantIntakeReadinessState dataMinimization,
        ApplicantIntakeReadinessState duplicateHandling,
        ApplicantIntakeReadinessState retention,
        ApplicantIntakeReadinessState evidence,
        ApplicantIntakeReadinessState applicantIdentity,
        ApplicantIntakeReadinessState publicUx,
        ApplicantIntakeReadinessState documentDependency,
        ApplicantIntakeReadinessState notificationDependency,
        IReadOnlyDictionary<string, ApplicantIntakeReadinessState> dependencyStates)
    {
        var required = new[]
        {
            sourceChannel,
            consent,
            dataMinimization,
            duplicateHandling,
            retention,
            evidence,
            applicantIdentity,
            publicUx,
            documentDependency,
            notificationDependency
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(ApplicantIntakeReadinessState state) =>
        state is ApplicantIntakeReadinessState.Ready or ApplicantIntakeReadinessState.NotRequired;

    private static void ValidateState(ApplicantIntakeReadinessState state, string fieldName, List<string> errors)
    {
        if (!Enum.IsDefined(state))
        {
            errors.Add($"{fieldName} is not supported.");
        }
    }

    private static void RequireText(string value, string fieldName, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        if (value.Trim().Length > maxLength)
        {
            errors.Add($"{fieldName} must be {maxLength} characters or fewer.");
        }
    }

    private static IEnumerable<string> ForbiddenValues(ApplicantIntakeCreateRequest request)
    {
        yield return request.Code;
        yield return request.DisplayName;
        yield return request.SourceContractVersion;
        yield return request.DeferredReason ?? string.Empty;

        foreach (var key in request.DependencyStates.Keys)
        {
            yield return key;
        }
    }

    private static bool ContainsForbiddenMarker(string value)
    {
        return !string.IsNullOrEmpty(value) && ForbiddenMarkerRegex.IsMatch(value);
    }

    private static string Normalize(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var index = 0;

        foreach (var current in value)
        {
            if (char.IsLetterOrDigit(current) || current == '_')
            {
                buffer[index++] = char.ToLowerInvariant(current);
            }
        }

        return new string(buffer[..index]);
    }
}
